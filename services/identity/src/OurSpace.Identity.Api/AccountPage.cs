using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;

namespace OurSpace.Identity.Api;

public static class AccountPage
{
    private const string Styles = """
        *{box-sizing:border-box;margin:0;padding:0}
        body{background:#0f0e17;color:#fffffe;font-family:ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;-webkit-font-smoothing:antialiased;min-height:100vh;display:flex;align-items:center;justify-content:center;padding:20px}
        .card{background:#1a1a2e;border:1px solid #2d2d44;border-radius:16px;padding:40px 36px;width:100%;max-width:400px;animation:fadeIn .2s ease}
        @keyframes fadeIn{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:translateY(0)}}
        .logo{font-size:13px;font-weight:700;color:#ff8906;letter-spacing:.12em;text-transform:uppercase;margin-bottom:28px;display:block}
        h1{font-size:1.6rem;font-weight:800;margin-bottom:6px}
        .sub{font-size:14px;color:#a7a9be;margin-bottom:32px;line-height:1.5}
        .notice{background:#1f1a0f;border:1px solid #4a3010;color:#ff8906;border-radius:8px;padding:12px 14px;margin-bottom:24px;font-size:13px;line-height:1.5}
        .error{background:#2a0d1a;border:1px solid #e53170;color:#ff6b9d;border-radius:8px;padding:12px 14px;margin-bottom:24px;font-size:13px;line-height:1.5}
        label{display:block;font-size:12px;font-weight:600;color:#a7a9be;text-transform:uppercase;letter-spacing:.08em;margin-bottom:6px}
        input{width:100%;background:#0f0e17;border:1px solid #2d2d44;color:#fffffe;border-radius:8px;padding:10px 13px;font-size:15px;outline:none;margin-bottom:20px;font-family:inherit}
        input:focus{border-color:#ff8906}
        button{width:100%;background:#ff8906;color:#0f0e17;font-weight:700;font-size:15px;padding:12px;border-radius:8px;border:none;cursor:pointer;margin-top:4px;font-family:inherit;transition:background .15s}
        button:hover{background:#e57a05}
        .links{margin-top:24px;text-align:center;font-size:13px;color:#a7a9be;line-height:2}
        .links a{color:#ff8906;text-decoration:none}
        .links a:hover{text-decoration:underline}
        """;

    public static string Form(HttpContext context, IAntiforgery antiforgery, string title,
        IEnumerable<(string Name, string Type, string Value)> fields,
        string? message = null, bool isError = false,
        string? sub = null, string? linksHtml = null)
    {
        var token = antiforgery.GetAndStoreTokens(context);
        var e = HtmlEncoder.Default;
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>")
            .Append(e.Encode(title)).Append(" · OurSpace</title><style>").Append(Styles).Append("</style></head><body><div class=\"card\">");
        html.Append("<span class=\"logo\">✦ OurSpace</span>");
        html.Append("<h1>").Append(e.Encode(title)).Append("</h1>");
        if (sub is not null) html.Append("<p class=\"sub\">").Append(e.Encode(sub)).Append("</p>");
        if (message is not null)
        {
            var cls = isError ? "error" : "notice";
            html.Append("<p role=\"status\" class=\"").Append(cls).Append("\">").Append(e.Encode(message)).Append("</p>");
        }

        html.Append("<form method=\"post\"><input type=\"hidden\" name=\"")
            .Append(e.Encode(token.FormFieldName)).Append("\" value=\"")
            .Append(e.Encode(token.RequestToken ?? string.Empty)).Append("\">");
        foreach (var field in fields)
        {
            if (field.Type == "hidden")
            {
                html.Append("<input type=\"hidden\" name=\"").Append(e.Encode(field.Name))
                    .Append("\" value=\"").Append(e.Encode(field.Value)).Append("\">");
                continue;
            }

            html.Append("<label for=\"f-").Append(e.Encode(field.Name)).Append("\">")
                .Append(e.Encode(field.Name)).Append("</label>")
                .Append("<input id=\"f-").Append(e.Encode(field.Name)).Append("\" name=\"")
                .Append(e.Encode(field.Name)).Append("\" type=\"").Append(e.Encode(field.Type))
                .Append("\" value=\"").Append(e.Encode(field.Value)).Append("\" autocomplete=\"")
                .Append(field.Type == "password" ? "current-password" : "email")
                .Append("\">");
        }

        html.Append("<button type=\"submit\">Continue</button></form>");
        if (linksHtml is not null) html.Append("<div class=\"links\">").Append(linksHtml).Append("</div>");
        html.Append("</div></body></html>");
        return html.ToString();
    }

    public static string Info(string title, string? message = null, bool isError = false,
        string? sub = null, string? linksHtml = null)
    {
        var e = HtmlEncoder.Default;
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>")
            .Append(e.Encode(title)).Append(" · OurSpace</title><style>").Append(Styles).Append("</style></head><body><div class=\"card\">");
        html.Append("<span class=\"logo\">✦ OurSpace</span>");
        html.Append("<h1>").Append(e.Encode(title)).Append("</h1>");
        if (sub is not null) html.Append("<p class=\"sub\">").Append(e.Encode(sub)).Append("</p>");
        if (message is not null)
        {
            var cls = isError ? "error" : "notice";
            html.Append("<p role=\"status\" class=\"").Append(cls).Append("\">").Append(e.Encode(message)).Append("</p>");
        }
        if (linksHtml is not null) html.Append("<div class=\"links\">").Append(linksHtml).Append("</div>");
        html.Append("</div></body></html>");
        return html.ToString();
    }
}
