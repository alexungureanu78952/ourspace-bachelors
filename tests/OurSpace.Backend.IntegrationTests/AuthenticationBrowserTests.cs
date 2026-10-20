extern alias BffApi;
extern alias IdentityApi;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OurSpace.Bff.Infrastructure;
using OurSpace.Profiles.Application;
using Xunit;

namespace OurSpace.Backend.IntegrationTests;

internal sealed class CapturedAccountEmail : IdentityApi::OurSpace.Identity.Api.IAccountEmailSender
{
    internal ConcurrentDictionary<string, string> Links { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Task SendAsync(string recipient, string subject, string link, CancellationToken cancellationToken)
    {
        Links[recipient] = link;
        return Task.CompletedTask;
    }
}

public sealed class AuthenticationBrowserTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task RealBrowserCompletesVerifiedLoginRefreshResetAndLogout()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var alternatives = new SubjectAlternativeNameBuilder();
        alternatives.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(alternatives.Build());
        using var createdCertificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(createdCertificate.Export(X509ContentType.Pfx), null);
        await using var identityBridge = await HttpsTestBridge.StartAsync(certificate);
        await using var bffBridge = await HttpsTestBridge.StartAsync(certificate);
        await using var profilesBridge = await HttpsTestBridge.StartAsync(certificate);
        var mail = new CapturedAccountEmail();
        const string secret = "test-only-confidential-client-secret-not-for-deployment";
        await using var identity = new WebApplicationFactory<IdentityApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Identity"] = postgres.IdentityConnection,
                ["Identity:Issuer"] = identityBridge.Origin,
                ["Identity:BffOrigin"] = bffBridge.Origin,
                ["Identity:ClientSecret"] = secret,
            }));
            builder.ConfigureTestServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddOpenIddict().AddServer(options => options.AddEphemeralSigningKey().AddEphemeralEncryptionKey());
                services.AddSingleton<IdentityApi::OurSpace.Identity.Api.IAccountEmailSender>(mail);
                services.Configure<Microsoft.AspNetCore.Builder.ExceptionHandlerOptions>(options => options.ExceptionHandler = async context =>
                {
                    var failure = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                    await context.Response.WriteAsync(failure?.Error.ToString() ?? "Unknown test server failure");
                });
            });
        });
        using var identityClient = identity.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(identityBridge.Origin) });
        identityBridge.Connect(identity.Server.CreateHandler());
        identityBridge.TestEndpoint = async context =>
        {
            if (context.Request.Path != "/__test/mail") return false;
            var address = context.Request.Query["email"].ToString();
            if (mail.Links.TryGetValue(address, out var link)) await context.Response.WriteAsJsonAsync(new { link });
            else context.Response.StatusCode = 404;
            return true;
        };

        await using var profiles = new ProfileApiFactory(postgres.ProfilesConnection, fakeAuthentication: false);
        await using var profilesHost = profiles.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = identityBridge.Origin,
            }));
            builder.ConfigureTestServices(services => services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                options => options.BackchannelHttpHandler = identity.Server.CreateHandler()));
        });
        using var profilesClient = profilesHost.CreateClient();
        profilesBridge.Connect(profilesHost.Server.CreateHandler());

        await using var bff = new WebApplicationFactory<BffApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Bff"] = postgres.BffConnection,
                ["Bff:Authority"] = identityBridge.Origin,
                ["Bff:ClientSecret"] = secret,
                ["Bff:ProfilesOrigin"] = profilesBridge.Origin,
            }));
            builder.ConfigureTestServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme,
                    options =>
                    {
                        options.BackchannelHttpHandler = identity.Server.CreateHandler();
                        options.Events.OnRemoteFailure = async context =>
                        {
                            context.HandleResponse();
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsync(context.Failure?.ToString() ?? "Unknown protocol failure");
                        };
                    });
                services.AddHttpClient("identity").ConfigurePrimaryHttpMessageHandler(identity.Server.CreateHandler);
                services.AddHttpClient("profiles").ConfigurePrimaryHttpMessageHandler(profilesHost.Server.CreateHandler);
                services.Configure<Microsoft.AspNetCore.Builder.ExceptionHandlerOptions>(options => options.ExceptionHandler = async context =>
                {
                    var failure = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                    await context.Response.WriteAsync(failure?.Error.ToString() ?? "Unknown test server failure");
                });
            });
        });
        using var bffClient = bff.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(bffBridge.Origin) });
        bffBridge.Connect(bff.Server.CreateHandler());
        bffBridge.TestEndpoint = async context =>
        {
            if (context.Request.Path != "/__test/expire-access") return false;
            using var scope = bff.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<SessionDbContext>();
            var row = await database.Sessions.SingleAsync();
            var store = bff.Services.GetRequiredService<PostgresTicketStore>();
            var ticket = await store.RetrieveAsync(row.Id);
            Assert.NotNull(ticket);
            ticket.Properties.UpdateTokenValue("expires_at", DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o"));
            await store.RenewAsync(row.Id, ticket);
            context.Response.StatusCode = 204;
            return true;
        };
        var root = FindRepository();
        var start = new ProcessStartInfo("node") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("scripts/test-auth-browser.mjs");
        start.Environment["OURSPACE_IDENTITY_ORIGIN"] = identityBridge.Origin;
        start.Environment["OURSPACE_BFF_ORIGIN"] = bffBridge.Origin;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Browser test could not start.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        var output = await outputTask;
        var errors = await errorTask;
        Assert.True(process.ExitCode == 0, errors);
        using var result = JsonDocument.Parse(output);
        var cookie = result.RootElement.GetProperty("cookie").GetString()!;
        var options = bff.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var browserTicket = options.TicketDataFormat.Unprotect(cookie);
        Assert.NotNull(browserTicket);
        Assert.Null(browserTicket.Properties.GetTokenValue("access_token"));
        Assert.Null(browserTicket.Properties.GetTokenValue("refresh_token"));
        using var verificationScope = bff.Services.CreateScope();
        Assert.Empty(await verificationScope.ServiceProvider.GetRequiredService<SessionDbContext>().Sessions.ToListAsync());
    }

    [Fact]
    public async Task ServerTicketsAreEncryptedExpireAndCannotBeResurrectedAfterRemoval()
    {
        var services = new ServiceCollection();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddDbContextFactory<SessionDbContext>(options => options.UseNpgsql(postgres.BffConnection));
        services.AddSingleton<PostgresTicketStore>();
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<PostgresTicketStore>();
        var ticket = new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity("test")),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1) }, CookieAuthenticationDefaults.AuthenticationScheme);
        ticket.Properties.StoreTokens([new AuthenticationToken { Name = "refresh_token", Value = "test-only-secret-refresh-token" }]);
        var key = await store.StoreAsync(ticket);
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SessionDbContext>();
        var row = await database.Sessions.AsNoTracking().SingleAsync(record => record.Id == key);
        Assert.DoesNotContain("test-only-secret-refresh-token", System.Text.Encoding.UTF8.GetString(row.Payload), StringComparison.Ordinal);
        Assert.Equal("test-only-secret-refresh-token", (await store.RetrieveAsync(key))!.Properties.GetTokenValue("refresh_token"));
        ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await store.RenewAsync(key, ticket);
        Assert.Null(await store.RetrieveAsync(key));
        await store.RemoveAsync(key);
        ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        await store.RenewAsync(key, ticket);
        Assert.Null(await store.RetrieveAsync(key));
    }

    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OurSpace.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
