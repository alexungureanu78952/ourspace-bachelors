using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OurSpace.Identity.Api;

public sealed class IdentityClientRegistration(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<IdentityProtocolSettings>>().Value;
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = IdentityProtocolSettings.ClientId,
            ClientSecret = settings.ClientSecret,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "OurSpace trusted browser backend",
        };
        descriptor.RedirectUris.Add(new Uri(settings.BffOrigin + "/signin-oidc"));
        descriptor.PostLogoutRedirectUris.Add(new Uri(settings.BffOrigin + "/signout-callback-oidc"));
        descriptor.Permissions.UnionWith([
            Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.EndSession,
            Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code, Permissions.Scopes.Profile,
            Permissions.Prefixes.Scope + IdentityProtocolSettings.ProfilesScope,
        ]);
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        var existing = await manager.FindByClientIdAsync(descriptor.ClientId, cancellationToken);
        if (existing is null)
        {
            await manager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(existing, descriptor, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
