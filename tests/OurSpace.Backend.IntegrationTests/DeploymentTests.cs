extern alias IdentityApi;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using OurSpace.Hosting;
using Xunit;

namespace OurSpace.Backend.IntegrationTests;

public sealed class DeploymentTests
{
    [Fact]
    public void ProductionRequiresEncryptedPersistentKeys()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        Assert.Throws<InvalidOperationException>(() => builder.AddPersistentKeys("OurSpace.DeploymentTests"));
    }

    [Fact]
    public async Task ProductionCannotDisableSmtpRevocationChecks()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Email:Host"] = "mailpit", ["Email:CheckCertificateRevocation"] = "false" });
        var sender = new IdentityApi::OurSpace.Identity.Api.SmtpAccountEmailSender(builder.Configuration, builder.Environment);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync("test@example.test", "test", "https://localhost/", CancellationToken.None));
        Assert.Contains("LocalDocker", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("172.29.249.10", "https")]
    [InlineData("172.29.249.99", "http")]
    public async Task ForwardedSchemeRequiresAnExplicitlyTrustedProxy(string peer, string expected)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Proxy:KnownProxies:0"] = "172.29.249.10" });
        builder.AddDeploymentConfiguration();
        await using var app = builder.Build();
        app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse(peer); return next(context); });
        app.UseForwardedHeaders();
        app.MapGet("/", (HttpContext context) => Results.Text(context.Request.Scheme));
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, await response.Content.ReadAsStringAsync());
    }
}
