using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;

namespace OurSpace.Bff.Api;

public sealed record DemoProfile(Guid Id, Guid OwnerId, string DisplayName, DateTimeOffset CreatedAt);

public static class DemoPage
{
    public static IResult Render(HttpContext context, IAntiforgery antiforgery, string authority,
        DemoProfile? profile = null, string? error = null, string profileId = "", int statusCode = 200)
    {
        var html = new StringBuilder("""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>OurSpace</title></head><body><main><h1>OurSpace</h1>
            """);
        if (context.User.Identity?.IsAuthenticated != true)
        {
            html.Append("<p>Sign in to create and load your private profiles.</p><a href=\"/bff/login\">Sign in</a><p><a href=\"")
                .Append(Encode(authority + "/account/register")).Append("\">Create account</a> | <a href=\"")
                .Append(Encode(authority + "/account/forgot")).Append("\">Reset password</a></p>");
        }
        else
        {
            var token = antiforgery.GetAndStoreTokens(context);
            var hidden = "<input type=\"hidden\" name=\"" + Encode(token.FormFieldName) + "\" value=\"" + Encode(token.RequestToken ?? string.Empty) + "\">";
            html.Append("<p>Signed in as ").Append(Encode(context.User.Identity.Name ?? "an OurSpace member")).Append(".</p>")
                .Append("<form method=\"post\" action=\"/bff/logout\">").Append(hidden).Append("<button>Sign out</button></form>");
            if (error is not null) html.Append("<p role=\"alert\">").Append(Encode(error)).Append("</p>");
            if (profile is not null)
            {
                html.Append("<section aria-labelledby=\"loaded-heading\"><h2 id=\"loaded-heading\">Your private profile</h2><p role=\"status\">Profile loaded.</p><dl>")
                    .Append("<dt>Display name</dt><dd>").Append(Encode(profile.DisplayName)).Append("</dd>")
                    .Append("<dt>Profile ID</dt><dd id=\"profile-id\">").Append(profile.Id).Append("</dd>")
                    .Append("<dt>Owner account</dt><dd>").Append(profile.OwnerId).Append("</dd>")
                    .Append("<dt>Created (UTC)</dt><dd>").Append(Encode(profile.CreatedAt.UtcDateTime.ToString("u", CultureInfo.InvariantCulture))).Append("</dd></dl></section>");
            }
            html.Append("""
                <section aria-labelledby="create-heading"><h2 id="create-heading">Create a private profile</h2><form method="post" action="/bff/demo/profiles">
                """).Append(hidden).Append("""
                <p><label for="display-name">Display name</label><br><input id="display-name" name="displayName" required maxlength="200"></p><button>Create profile</button></form></section>
                <section aria-labelledby="load-heading"><h2 id="load-heading">Load a private profile</h2><p>Use the profile ID shown after creation. Only its owner can load it.</p><form method="get" action="/"><p><label for="lookup-id">Profile ID</label><br><input id="lookup-id" name="profileId" required maxlength="36" value="
                """).Append(Encode(profile?.Id.ToString() ?? profileId)).Append("""
                "></p><button>Load profile</button></form></section>
                """);
        }
        return Results.Content(html.Append("</main></body></html>").ToString(), "text/html; charset=utf-8", statusCode: statusCode);
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
