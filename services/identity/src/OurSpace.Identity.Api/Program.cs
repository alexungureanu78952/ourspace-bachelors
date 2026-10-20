using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using OurSpace.Hosting;
using OurSpace.Identity.Api;
using OurSpace.Identity.Infrastructure;

if (await Deployment.TryHealthCheckAsync(args)) return;
var builder = WebApplication.CreateBuilder(args);
builder.AddDeploymentConfiguration();
builder.AddPersistentKeys("OurSpace.Identity");
builder.Services.AddProblemDetails();
builder.Logging.AddFilter("OpenIddict", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute())).AddApplicationPart(typeof(AccountController).Assembly);
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-OurSpace.Identity.Csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddOptions<IdentityProtocolSettings>().BindConfiguration("Identity")
    .Validate(settings => Uri.TryCreate(settings.Issuer, UriKind.Absolute, out var issuer) && issuer.Scheme == "https" && issuer.Query.Length == 0 && issuer.Fragment.Length == 0 && issuer.UserInfo.Length == 0, "Identity:Issuer must be an HTTPS URL.")
    .Validate(settings => Uri.TryCreate(settings.BffOrigin, UriKind.Absolute, out var origin) && origin.Scheme == "https" && origin.Origin() == settings.BffOrigin && origin.UserInfo.Length == 0, "Identity:BffOrigin must be an exact HTTPS origin.")
    .Validate(settings => settings.ClientSecret.Length >= 32, "A confidential client secret of at least 32 characters is required.")
    .ValidateOnStart();
builder.Services.AddIdentityFoundation((services, options) =>
{
    var connection = services.GetRequiredService<IConfiguration>().GetConnectionString("Identity");
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:Identity is required.");
    options.UseNpgsql(connection);
});
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-OurSpace.Identity";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/account/login";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = false;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddAuthorization();
if (!builder.Configuration.GetValue("Email:CheckCertificateRevocation", true) && !builder.Environment.IsEnvironment("LocalDocker"))
    throw new InvalidOperationException("Disabling SMTP revocation checks is permitted only in LocalDocker.");
builder.Services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<IdentityStoreContext>())
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetTokenEndpointUris("/connect/token");
        options.SetEndSessionEndpointUris("/connect/logout");
        options.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow();
        options.RequireProofKeyForCodeExchange();
        options.RegisterScopes("openid", "profile", "offline_access", IdentityProtocolSettings.ProfilesScope);
        options.DisableAccessTokenEncryption();
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(5));
        options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(5));
        options.SetRefreshTokenLifetime(TimeSpan.FromMinutes(30));
        options.DisableSlidingRefreshTokenExpiration();
        options.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough().EnableEndSessionEndpointPassthrough();
        if (builder.Environment.IsDevelopment() && builder.Configuration["Identity:SigningCertificate"] is null) options.AddDevelopmentSigningCertificate().AddDevelopmentEncryptionCertificate();
    });
builder.Services.AddOptions<OpenIddictServerOptions>().Configure<IConfiguration>((options, configuration) =>
{
    options.Issuer = new Uri(configuration["Identity:Issuer"] ?? throw new InvalidOperationException("Identity:Issuer is required."));
    if (configuration["Identity:SigningCertificate"] is { Length: > 0 } signing)
    {
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(signing, configuration["Identity:SigningCertificatePassword"]);
        options.SigningCredentials.Add(new SigningCredentials(new X509SecurityKey(certificate), SecurityAlgorithms.RsaSha256));
    }

    if (configuration["Identity:EncryptionCertificate"] is { Length: > 0 } encryption)
    {
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(encryption, configuration["Identity:EncryptionCertificatePassword"]);
        options.EncryptionCredentials.Add(new EncryptingCredentials(new X509SecurityKey(certificate), SecurityAlgorithms.RsaOAEP, SecurityAlgorithms.Aes256CbcHmacSha512));
    }
}).Validate(options => options.SigningCredentials.Count > 0 && options.EncryptionCredentials.Count > 0, "Configure signing/encryption certificates outside Development.").ValidateOnStart();
builder.Services.AddHostedService<IdentityClientRegistration>();
builder.Services.AddHostedService<ProtocolMaintenance>();
var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    var bffOrigin = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityProtocolSettings>>().Value.BffOrigin;
    context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; form-action 'self' " + bffOrigin + "; frame-ancestors 'none'; base-uri 'none'";
    await next(context);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();
app.MapGet("/", () => Results.Content("<!doctype html><html lang=\"en\"><title>OurSpace accounts</title><main><h1>OurSpace accounts</h1><a href=\"/account/register\">Create account</a><br><a href=\"/account/login\">Sign in</a><br><a href=\"/account/forgot\">Reset password</a></main></html>", "text/html"));
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (IdentityStoreContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.Run();

#pragma warning disable CA1050 // ASP.NET Core test-host entry point.
public partial class Program;
#pragma warning restore CA1050
