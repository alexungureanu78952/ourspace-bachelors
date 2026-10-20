using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace OurSpace.Bff.Api;

public static class DemoEndpoints
{
    public static async Task<IResult> LoadAsync(string? profileId, HttpContext context, IAntiforgery antiforgery,
        ProfilesBackend profiles, IOptions<BffSettings> settings)
    {
        if (context.User.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(profileId))
            return DemoPage.Render(context, antiforgery, settings.Value.Authority);
        if (!Guid.TryParse(profileId, out var id) || id == Guid.Empty)
            return DemoPage.Render(context, antiforgery, settings.Value.Authority, error: "Enter a valid profile ID.", profileId: profileId, statusCode: 400);
        try
        {
            using var response = await profiles.GetAsync(context, id);
            if (response is null) return Results.Redirect("/");
            if (!response.IsSuccessStatusCode)
                return Failure(context, antiforgery, settings.Value.Authority, response.StatusCode, profileId);
            var profile = await response.Content.ReadFromJsonAsync<DemoProfile>(context.RequestAborted);
            return profile is null ? Failure(context, antiforgery, settings.Value.Authority, HttpStatusCode.BadGateway, profileId) :
                DemoPage.Render(context, antiforgery, settings.Value.Authority, profile);
        }
        catch (Exception exception) when (IsServiceFailure(exception, context))
        {
            return Failure(context, antiforgery, settings.Value.Authority, HttpStatusCode.ServiceUnavailable, profileId);
        }
    }

    public static async Task<IResult> CreateAsync(HttpContext context, IAntiforgery antiforgery,
        ProfilesBackend profiles, IOptions<BffSettings> settings)
    {
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        try
        {
            using var response = await profiles.CreateAsync(context, new CreateProfileRequest(form["displayName"].ToString()));
            if (response is null) return Results.Redirect("/");
            if (!response.IsSuccessStatusCode)
                return Failure(context, antiforgery, settings.Value.Authority, response.StatusCode);
            var profile = await response.Content.ReadFromJsonAsync<DemoProfile>(context.RequestAborted);
            return profile is null ? Failure(context, antiforgery, settings.Value.Authority, HttpStatusCode.BadGateway) :
                Results.Redirect("/?profileId=" + profile.Id);
        }
        catch (Exception exception) when (IsServiceFailure(exception, context))
        {
            return Failure(context, antiforgery, settings.Value.Authority, HttpStatusCode.ServiceUnavailable);
        }
    }

    private static bool IsServiceFailure(Exception exception, HttpContext context) =>
        exception is HttpRequestException or JsonException || exception is TaskCanceledException && !context.RequestAborted.IsCancellationRequested;

    private static IResult Failure(HttpContext context, IAntiforgery antiforgery, string authority, HttpStatusCode status, string profileId = "")
    {
        if (status == HttpStatusCode.Unauthorized) return Results.Redirect("/bff/login");
        var (message, code) = status switch
        {
            HttpStatusCode.BadRequest => ("Use a display name of 1-200 characters without control characters.", 400),
            HttpStatusCode.NotFound => ("Profile not found or unavailable to this account.", 404),
            HttpStatusCode.Forbidden => ("You cannot access this profile.", 403),
            HttpStatusCode.Conflict => ("This account already has a profile. Load your existing profile using its ID.", 409),
            _ => ("The profile service is unavailable. Please try again.", 503),
        };
        return DemoPage.Render(context, antiforgery, authority, error: message, profileId: profileId, statusCode: code);
    }
}
