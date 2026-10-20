using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OurSpace.Identity.Infrastructure;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OurSpace.Identity.Api;

public sealed class ProtocolController(
    UserManager<IdentityUserAccount> users,
    SignInManager<IdentityUserAccount> signIn,
    IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations,
    IAntiforgery antiforgery) : Controller
{
    private const string Stamp = "ourspace:security_stamp";

    [HttpGet("/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Missing validated authorization request.");
        var session = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var fresh = session.Succeeded && !(request.HasPromptValue(PromptValues.Login) ||
            request.MaxAge is { } age && session.Properties?.IssuedUtc is { } issued && DateTimeOffset.UtcNow - issued > TimeSpan.FromSeconds(age));
        if (!fresh)
        {
            if (request.HasPromptValue(PromptValues.None)) return Deny(Errors.LoginRequired);
            return Challenge(new AuthenticationProperties { RedirectUri = Request.GetEncodedPathAndQuery() }, IdentityConstants.ApplicationScheme);
        }

        var account = await users.GetUserAsync(session.Principal!);
        if (account is null || !await signIn.CanSignInAsync(account) || await users.IsLockedOutAsync(account)) return Deny(Errors.AccessDenied);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role));
        principal.SetClaim(Claims.Subject, account.Id.ToString());
        principal.SetClaim(Claims.Name, account.UserName);
        principal.SetClaim(Stamp, await users.GetSecurityStampAsync(account));
        principal.SetScopes(request.GetScopes());
        principal.SetResources(IdentityProtocolSettings.ProfilesScope);
        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(claim.Type is Claims.Subject or Claims.Name ? [Destinations.AccessToken, Destinations.IdentityToken] : []);
        }

        var client = await applications.FindByClientIdAsync(request.ClientId!) ?? throw new InvalidOperationException("Unknown validated client.");
        var authorization = await authorizations.CreateAsync(principal, account.Id.ToString(), (await applications.GetIdAsync(client))!,
            AuthorizationTypes.AdHoc, principal.GetScopes());
        principal.SetAuthorizationId(await authorizations.GetIdAsync(authorization));
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("/connect/token"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange()
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal;
        var account = principal is null ? null : await users.FindByIdAsync(principal.GetClaim(Claims.Subject)!);
        if (principal is null || account is null || !await signIn.CanSignInAsync(account) || await users.IsLockedOutAsync(account) ||
            principal!.GetClaim(Stamp) != await users.GetSecurityStampAsync(account))
        {
            return Deny(Errors.InvalidGrant);
        }

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("/connect/logout")]
    public IActionResult Logout()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Missing validated logout request.");
        var fields = new List<(string Name, string Type, string Value)>();
        foreach (var name in new[] { "client_id", "id_token_hint", "post_logout_redirect_uri", "state" })
        {
            if ((string?)request.GetParameter(name) is { Length: > 0 } value) fields.Add((name, "hidden", value));
        }
        return Content(AccountPage.Form(HttpContext, antiforgery, "Sign out of OurSpace", fields), "text/html; charset=utf-8");
    }

    [HttpPost("/connect/logout")]
    public async Task<IActionResult> EndSession()
    {
        var account = await users.GetUserAsync(User);
        if (account is not null) await authorizations.RevokeBySubjectAsync(account.Id.ToString());
        await signIn.SignOutAsync();
        return SignOut(new AuthenticationProperties(), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private ForbidResult Deny(string error) => Forbid(new AuthenticationProperties(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
    }), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
