using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OurSpace.Hosting;
using OurSpace.Profiles.Api;
using OurSpace.Profiles.Api.Security;
using OurSpace.Profiles.Application;
using OurSpace.Profiles.Domain;
using OurSpace.Profiles.Infrastructure;

if (await Deployment.TryHealthCheckAsync(args)) return;
var builder = WebApplication.CreateBuilder(args);
builder.AddDeploymentConfiguration();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProfileExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IProfileStore, EfProfileStore>();
builder.Services.AddScoped<ProfileOperations>();
builder.Services.AddDbContext<ProfilesDbContext>((services, options) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var connection = configuration.GetConnectionString("Profiles");
    if (string.IsNullOrWhiteSpace(connection))
    {
        throw new InvalidOperationException("ConnectionStrings:Profiles must be configured.");
    }

    options.UseNpgsql(connection);
});
builder.Services.AddOptions<AuthenticationSettings>()
    .BindConfiguration("Authentication")
    .Validate(settings => Uri.TryCreate(settings.Authority, UriKind.Absolute, out var authority) &&
        authority.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(authority.UserInfo),
        "Authentication:Authority must be an HTTPS issuer URL.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Audience), "Authentication:Audience is required.")
    .ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthenticationSettings>>((options, settings) =>
    {
        options.Authority = settings.Value.Authority;
        options.Audience = settings.Value.Audience;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.RequireSignedTokens = true;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build())
    .AddPolicy("StableUser", policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
        Guid.TryParse(context.User.FindFirst("sub")?.Value, out var id) && id != Guid.Empty));

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
app.MapGet("/health/ready", async (ProfilesDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable)).AllowAnonymous();

var profiles = app.MapGroup("/api/v1/profiles").RequireAuthorization("StableUser");
profiles.MapPost("/", async (CreateProfileRequest request, ProfileOperations operations, CancellationToken cancellationToken) =>
{
    if (request.DisplayName is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["displayName"] = ["Display name is required."] });
    }

    var profile = await operations.CreateAsync(request.DisplayName, cancellationToken);
    return Results.Created($"/api/v1/profiles/{profile.Id}", profile);
});
profiles.MapGet("/{profileId:guid}", async (Guid profileId, ProfileOperations operations, CancellationToken cancellationToken) =>
{
    var profile = await operations.GetAsync(profileId, cancellationToken);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
});
app.Run();

#pragma warning disable CA1050 // Top-level entry-point hook for the ASP.NET Core test host.
public partial class Program;
#pragma warning restore CA1050
