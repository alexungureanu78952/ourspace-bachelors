using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;

namespace OurSpace.Bff.Api;

public sealed record DemoProfile(Guid Id, Guid OwnerId, string DisplayName, string Bio, DateTimeOffset CreatedAt);

public static class DemoPage
{
    private const string Styles = """
        <style>
        *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: system-ui, sans-serif; background: #0f0e17; color: #fffffe; min-height: 100vh; }
        main { max-width: 760px; margin: 0 auto; padding: 32px 16px 64px; }
        h1 { font-size: 2rem; font-weight: 700; margin-bottom: 8px; }
        .tagline { color: #a7a9be; margin-bottom: 32px; font-size: 0.95rem; }
        .card { background: #1a1a2e; border: 1px solid #2a2a4a; border-radius: 12px; padding: 24px; margin-bottom: 20px; }
        .card h2 { font-size: 1.1rem; font-weight: 600; margin-bottom: 16px; color: #ff8906; }
        label { display: block; font-size: 0.875rem; color: #a7a9be; margin-bottom: 6px; }
        input[type=text], textarea {
            width: 100%; padding: 10px 14px; border-radius: 8px;
            border: 1px solid #2a2a4a; background: #0f0e17; color: #fffffe;
            font-size: 0.95rem; outline: none; transition: border-color 0.15s;
        }
        input[type=text]:focus, textarea:focus { border-color: #ff8906; }
        textarea { resize: vertical; min-height: 80px; font-family: inherit; }
        button {
            margin-top: 14px; padding: 10px 24px; border-radius: 8px; border: none;
            background: #ff8906; color: #0f0e17; font-weight: 600; font-size: 0.95rem;
            cursor: pointer; transition: opacity 0.15s;
        }
        button:hover { opacity: 0.85; }
        .sign-out-btn { background: transparent; border: 1px solid #2a2a4a; color: #a7a9be; margin-top: 0; padding: 6px 16px; font-size: 0.85rem; }
        .topbar { display: flex; justify-content: space-between; align-items: center; margin-bottom: 32px; padding-bottom: 16px; border-bottom: 1px solid #2a2a4a; }
        .topbar .user { font-size: 0.9rem; color: #a7a9be; }
        .alert { padding: 12px 16px; border-radius: 8px; background: #3a1a2a; border: 1px solid #ff4466; color: #ff8899; font-size: 0.9rem; margin-bottom: 16px; }
        .meta { font-size: 0.8rem; color: #a7a9be; margin-top: 12px; }
        .meta span { margin-right: 16px; }
        .profile-frame { width: 100%; height: 340px; border: none; border-radius: 8px; margin-bottom: 10px; display: block; }
        .profile-id { font-family: monospace; font-size: 0.8rem; color: #a7a9be; word-break: break-all; }
        .links { display: flex; gap: 16px; margin-bottom: 32px; font-size: 0.9rem; }
        .links a { color: #ff8906; text-decoration: none; }
        .links a:hover { text-decoration: underline; }
        </style>
        """;

    public static IResult Render(HttpContext context, IAntiforgery antiforgery, string authority,
        DemoProfile? profile = null, string? error = null, string profileId = "", int statusCode = 200)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>OurSpace</title>")
            .Append(Styles)
            .Append("</head><body><main>");

        if (context.User.Identity?.IsAuthenticated != true)
        {
            html.Append("<h1>OurSpace</h1><p class=\"tagline\">A social platform for customizable AI-assisted profiles.</p>")
                .Append("<div class=\"links\"><a href=\"/bff/login\">Sign in</a><a href=\"")
                .Append(Encode(authority + "/account/register")).Append("\">Create account</a><a href=\"")
                .Append(Encode(authority + "/account/forgot")).Append("\">Reset password</a></div>");
        }
        else
        {
            var token = antiforgery.GetAndStoreTokens(context);
            var hidden = "<input type=\"hidden\" name=\"" + Encode(token.FormFieldName) + "\" value=\"" + Encode(token.RequestToken ?? string.Empty) + "\">";

            html.Append("<div class=\"topbar\"><h1>OurSpace</h1><div><span class=\"user\">")
                .Append(Encode(context.User.Identity.Name ?? "member"))
                .Append("</span><form method=\"post\" action=\"/bff/logout\" style=\"display:inline;margin-left:12px\">")
                .Append(hidden).Append("<button class=\"sign-out-btn\">Sign out</button></form></div></div>");

            if (error is not null)
                html.Append("<div class=\"alert\" role=\"alert\">").Append(Encode(error)).Append("</div>");

            if (profile is not null)
            {
                var viewUrl = "/bff/profiles/" + profile.Id + "/view";
                html.Append("<div class=\"card\"><h2>Your profile</h2>")
                    .Append("<iframe class=\"profile-frame\" src=\"").Append(Encode(viewUrl))
                    .Append("\" sandbox=\"allow-scripts\" title=\"Profile preview\"></iframe>")
                    .Append("<div class=\"meta\">")
                    .Append("<span class=\"profile-id\">ID: ").Append(profile.Id).Append("</span>")
                    .Append("<span>Owner: ").Append(profile.OwnerId).Append("</span>")
                    .Append("<span>Created: ").Append(Encode(profile.CreatedAt.UtcDateTime.ToString("u", CultureInfo.InvariantCulture))).Append("</span>")
                    .Append("</div></div>");
            }

            html.Append("<div class=\"card\"><h2>Create a profile</h2><form method=\"post\" action=\"/bff/demo/profiles\">")
                .Append(hidden)
                .Append("<p><label for=\"display-name\">Display name</label>")
                .Append("<input id=\"display-name\" name=\"displayName\" type=\"text\" required maxlength=\"200\"></p>")
                .Append("<p style=\"margin-top:14px\"><label for=\"bio\">Bio <span style=\"color:#a7a9be;font-weight:400\">(optional)</span></label>")
                .Append("<textarea id=\"bio\" name=\"bio\" maxlength=\"500\" placeholder=\"Tell people a bit about yourself...\"></textarea></p>")
                .Append("<button>Create profile</button></form></div>");

            html.Append("<div class=\"card\"><h2>Load a profile by ID</h2>")
                .Append("<p style=\"font-size:0.875rem;color:#a7a9be;margin-bottom:16px\">Only the profile's owner can load it.</p>")
                .Append("<form method=\"get\" action=\"/\">")
                .Append("<label for=\"lookup-id\">Profile ID</label>")
                .Append("<input id=\"lookup-id\" name=\"profileId\" type=\"text\" required maxlength=\"36\" value=\"")
                .Append(Encode(profile?.Id.ToString() ?? profileId)).Append("\">")
                .Append("<button>Load profile</button></form></div>");
        }

        html.Append("</main></body></html>");
        return Results.Content(html.ToString(), "text/html; charset=utf-8", statusCode: statusCode);
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
