using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using OurSpace.Bff.Infrastructure;

namespace OurSpace.Bff.Api;

public sealed class BackendTokens(PostgresTicketStore store, IHttpClientFactory clients, IOptions<BffSettings> settings)
{
    public async Task<string?> GetAsync(HttpContext context)
    {
        var authentication = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var key = authentication.Properties?.Items.TryGetValue(PostgresTicketStore.SessionKey, out var value) == true ? value : null;
        if (key is null) return null;
        return await store.WithLockedTicketAsync(key, async (ticket, cancellationToken) =>
        {
            if (DateTimeOffset.TryParse(ticket.Properties.GetTokenValue("expires_at"), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var expires) && expires > DateTimeOffset.UtcNow.AddSeconds(30))
            {
                return ticket.Properties.GetTokenValue("access_token");
            }

            var refresh = ticket.Properties.GetTokenValue("refresh_token");
            if (refresh is null) return null;
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.Value.Authority.TrimEnd('/') + "/connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refresh,
                    ["client_id"] = "ourspace-bff",
                    ["client_secret"] = settings.Value.ClientSecret,
                }),
            };
            using var response = await clients.CreateClient("identity").SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var access = payload.RootElement.GetProperty("access_token").GetString();
            if (string.IsNullOrEmpty(access)) return null;
            ticket.Properties.UpdateTokenValue("access_token", access);
            if (payload.RootElement.TryGetProperty("refresh_token", out var renewed))
            {
                ticket.Properties.UpdateTokenValue("refresh_token", renewed.GetString()!);
            }
            ticket.Properties.UpdateTokenValue("expires_at",
                DateTimeOffset.UtcNow.AddSeconds(payload.RootElement.GetProperty("expires_in").GetInt32()).ToString("o", CultureInfo.InvariantCulture));
            return access;
        }, context.RequestAborted);
    }
}
