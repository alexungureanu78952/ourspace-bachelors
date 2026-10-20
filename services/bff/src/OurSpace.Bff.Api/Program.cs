using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OurSpace.Bff.Api;
using OurSpace.Bff.Infrastructure;
using OurSpace.Hosting;

if (await Deployment.TryHealthCheckAsync(args)) return;
var builder = WebApplication.CreateBuilder(args);
builder.AddDeploymentConfiguration();
builder.AddPersistentKeys("OurSpace.Bff");
builder.Services.AddProblemDetails();
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.AddDataProtection().SetApplicationName("OurSpace.Bff");
builder.Services.AddOptions<BffSettings>().BindConfiguration("Bff")
    .Validate(settings => SecureOrigin(settings.Authority), "Bff:Authority must be an HTTPS origin.")
    .Validate(settings => SecureOrigin(settings.ProfilesOrigin), "Bff:ProfilesOrigin must be an HTTPS origin.")
    .Validate(settings => settings.ClientSecret.Length >= 32, "Bff:ClientSecret must contain at least 32 characters.").ValidateOnStart();
builder.Services.AddDbContextFactory<SessionDbContext>((services, options) =>
{
    options.UseNpgsql(services.GetRequiredService<IConfiguration>().GetConnectionString("Bff")
        ?? throw new InvalidOperationException("ConnectionStrings:Bff is required."));
});
builder.Services.AddSingleton<PostgresTicketStore>();
builder.Services.AddSingleton<ITicketStore>(services => services.GetRequiredService<PostgresTicketStore>());
builder.Services.AddScoped<BackendTokens>();
builder.Services.AddHostedService<SessionMaintenance>();
builder.Services.AddHttpClient("identity", client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient("profiles", client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-OurSpace.Bff";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
    }).AddOpenIdConnect();
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<ITicketStore>((options, store) => options.SessionStore = store);
builder.Services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
    .Configure<IOptions<BffSettings>>((options, settings) =>
    {
        options.Authority = settings.Value.Authority;
        options.ClientId = "ourspace-bff";
        options.ClientSecret = settings.Value.ClientSecret;
        options.ResponseType = "code";
        options.ResponseMode = "query";
        options.UsePkce = true;
        options.RequireHttpsMetadata = true;
        options.BackchannelHttpHandler = new SocketsHttpHandler { AllowAutoRedirect = false };
        options.TokenValidationParameters.NameClaimType = "name";
        options.SaveTokens = true;
        options.MapInboundClaims = false;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.UseTokenLifetime = false;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("offline_access");
        options.Scope.Add("ourspace.profiles");
        options.Events.OnTicketReceived = context =>
        {
            context.Properties!.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30);
            context.Properties!.AllowRefresh = false;
            return Task.CompletedTask;
        };
        options.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = 400;
            return context.Response.WriteAsync("Authentication could not be completed.");
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-OurSpace.Bff.Csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    var authority = context.RequestServices.GetRequiredService<IOptions<BffSettings>>().Value.Authority;
    context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; connect-src 'self'; form-action 'self' " + authority + "; frame-ancestors 'none'; base-uri 'none'";
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapGet("/health/ready", async (SessionDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/bff/login", (string? returnUrl) =>
    Results.Challenge(new AuthenticationProperties { RedirectUri = LocalPath(returnUrl) ? returnUrl : "/" }, [OpenIdConnectDefaults.AuthenticationScheme]));
app.MapGet("/", (HttpContext context, IAntiforgery antiforgery) =>
{
    var token = antiforgery.GetAndStoreTokens(context);
    return Results.Content("<!doctype html><html lang=\"en\"><title>OurSpace</title><main><h1>OurSpace</h1><a href=\"/bff/login\">Sign in</a>" +
        "<form method=\"post\" action=\"/bff/logout\"><input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"" +
        HtmlEncoder.Default.Encode(token.RequestToken ?? string.Empty) + "\"><button>Sign out</button></form></main></html>", "text/html");
});
var api = app.MapGroup("/bff").RequireAuthorization();
api.AddEndpointFilter(async (context, next) =>
{
    if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
    {
        try { await context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException) { return Results.BadRequest(new { error = "Invalid CSRF token." }); }
    }
    return await next(context);
});
api.MapGet("/me", (HttpContext context) => Results.Ok(new { userId = context.User.FindFirst("sub")?.Value }));
api.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) => Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }));
api.MapPost("/logout", () => Results.SignOut(new AuthenticationProperties { RedirectUri = "/" },
    [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
api.MapPost("/profiles", async (CreateProfileRequest input, HttpContext context, BackendTokens tokens, IHttpClientFactory clients, IOptions<BffSettings> settings) =>
    await Forward(context, tokens, clients, settings.Value, HttpMethod.Post, "/api/v1/profiles/", input));
api.MapGet("/profiles/{id:guid}", async (Guid id, HttpContext context, BackendTokens tokens, IHttpClientFactory clients, IOptions<BffSettings> settings) =>
    await Forward(context, tokens, clients, settings.Value, HttpMethod.Get, "/api/v1/profiles/" + id, null));
app.Run();

static bool SecureOrigin(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.GetLeftPart(UriPartial.Authority) == value && uri.UserInfo.Length == 0;
static bool LocalPath(string? value) => value is { Length: > 0 } && value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\') && !value.Any(char.IsControl);
static async Task<IResult> Forward(HttpContext context, BackendTokens tokens, IHttpClientFactory clients, BffSettings settings, HttpMethod method, string path, CreateProfileRequest? body)
{
    var token = await tokens.GetAsync(context);
    if (token is null)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Unauthorized();
    }
    using var request = new HttpRequestMessage(method, settings.ProfilesOrigin + path);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (body is not null) request.Content = JsonContent.Create(body);
    using var response = await clients.CreateClient("profiles").SendAsync(request, context.RequestAborted);
    var content = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
    return Results.Content(System.Text.Encoding.UTF8.GetString(content), "application/json", statusCode: (int)response.StatusCode);
}

#pragma warning disable CA1050 // Top-level entry-point test hook.
public partial class Program;
#pragma warning restore CA1050
