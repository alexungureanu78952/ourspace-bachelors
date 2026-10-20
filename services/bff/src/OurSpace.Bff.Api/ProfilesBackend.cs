using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace OurSpace.Bff.Api;

public sealed class ProfilesBackend(BackendTokens tokens, IHttpClientFactory clients, IOptions<BffSettings> settings)
{
    public Task<HttpResponseMessage?> CreateAsync(HttpContext context, CreateProfileRequest input) =>
        SendAsync(context, HttpMethod.Post, "/api/v1/profiles/", input);

    public Task<HttpResponseMessage?> GetAsync(HttpContext context, Guid id) =>
        SendAsync(context, HttpMethod.Get, "/api/v1/profiles/" + id, null);

    private async Task<HttpResponseMessage?> SendAsync(HttpContext context, HttpMethod method, string path, CreateProfileRequest? input)
    {
        var token = await tokens.GetAsync(context);
        if (token is null)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return null;
        }
        using var request = new HttpRequestMessage(method, settings.Value.ProfilesOrigin + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (input is not null) request.Content = JsonContent.Create(input);
        return await clients.CreateClient("profiles").SendAsync(request, context.RequestAborted);
    }
}
