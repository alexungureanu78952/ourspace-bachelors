using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;

namespace OurSpace.Identity.Api;

public static class AccountPage
{
    public static string Form(HttpContext context, IAntiforgery antiforgery, string title,
        IEnumerable<(string Name, string Type, string Value)> fields, string? message = null)
    {
        var token = antiforgery.GetAndStoreTokens(context);
        var html = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>");
        html.Append(HtmlEncoder.Default.Encode(title)).Append("</title></head><body><main><h1>")
            .Append(HtmlEncoder.Default.Encode(title)).Append("</h1>");
        if (message is not null)
        {
            html.Append("<p role=\"status\">").Append(HtmlEncoder.Default.Encode(message)).Append("</p>");
        }

        html.Append("<form method=\"post\"><input type=\"hidden\" name=\"")
            .Append(HtmlEncoder.Default.Encode(token.FormFieldName)).Append("\" value=\"")
            .Append(HtmlEncoder.Default.Encode(token.RequestToken ?? string.Empty)).Append("\">");
        foreach (var field in fields)
        {
            var label = HtmlEncoder.Default.Encode(field.Name);
            if (field.Type != "hidden") html.Append("<label>").Append(label);
            html.Append("<input name=\"").Append(label).Append("\" type=\"")
                .Append(HtmlEncoder.Default.Encode(field.Type)).Append("\" value=\"")
                .Append(HtmlEncoder.Default.Encode(field.Value)).Append("\">");
            if (field.Type != "hidden") html.Append("</label><br>");
        }

        return html.Append("<button type=\"submit\">Continue</button></form></main></body></html>").ToString();
    }
}
