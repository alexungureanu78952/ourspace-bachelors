using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OurSpace.Identity.Infrastructure;

namespace OurSpace.Identity.Api;

public sealed class RegisterInput
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginInput
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;
    [Required, StringLength(128)]
    public string Password { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "/";
}

public sealed class RecoveryInput
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;
}

public sealed class TokenInput
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    [Required, StringLength(4096)]
    public string Token { get; set; } = string.Empty;
    public string? Password { get; set; }
}

[Route("account")]
public sealed class AccountController(
    UserManager<IdentityUserAccount> users,
    SignInManager<IdentityUserAccount> signIn,
    IAccountEmailSender emailSender,
    IAntiforgery antiforgery,
    IOptions<IdentityProtocolSettings> settings,
    IOpenIddictAuthorizationManager authorizations) : Controller
{
    [HttpGet("register")]
    public IActionResult Register([FromQuery] string? locale = null)
    {
        var s = AccountLocale.For(locale ?? "en");
        return Page(s.CreateAccount, [("Email", "email", ""), ("Password", "password", "")],
            sub: s.CreateAccountSub,
            linksHtml: $"{s.AlreadyHaveAccount} <a href=\"/account/login{LocaleQuery(locale)}\">{s.SignInLink}</a>");
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] RegisterInput input, [FromQuery] string? locale, CancellationToken cancellationToken)
    {
        var s = AccountLocale.For(locale ?? "en");
        var loginLinks = $"{s.AlreadyHaveAccount} <a href=\"/account/login{LocaleQuery(locale)}\">{s.SignInLink}</a>";
        if (!ModelState.IsValid)
            return Page(s.CreateAccount, [("Email", "email", ""), ("Password", "password", "")],
                message: s.CreateAccountInvalid, isError: true, linksHtml: loginLinks);

        var address = input.Email.Trim();
        var candidate = new IdentityUserAccount { Id = Guid.NewGuid(), UserName = address, Email = address };
        foreach (var validator in users.PasswordValidators)
        {
            if (!(await validator.ValidateAsync(users, candidate, input.Password)).Succeeded)
                return Page(s.CreateAccount, [("Email", "email", ""), ("Password", "password", "")],
                    message: s.CreateAccountWeakPassword, isError: true, linksHtml: loginLinks);
        }

        var account = await users.FindByEmailAsync(address);
        if (account is null && (await users.CreateAsync(candidate, input.Password)).Succeeded) account = candidate;
        if (account is { EmailConfirmed: false } && await users.CheckPasswordAsync(account, input.Password))
        {
            var token = await users.GenerateEmailConfirmationTokenAsync(account);
            await emailSender.SendAsync(address, "Confirm your OurSpace email",
                Link("/account/confirm", account.Id, token), cancellationToken);
        }

        return InfoPage(s.CheckEmail,
            message: s.CheckEmailMessage,
            linksHtml: $"{s.BackToSignIn} <a href=\"{BffLogin}\">{s.SignInLink}</a>");
    }

    [HttpGet("confirm")]
    public IActionResult Confirm([FromQuery] string userId, [FromQuery] string token, [FromQuery] string? locale = null)
    {
        var s = AccountLocale.For(locale ?? "en");
        return Page(s.ConfirmEmail, [("UserId", "hidden", userId), ("Token", "hidden", token)],
            sub: s.ConfirmEmailSub);
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromForm] TokenInput input, [FromQuery] string? locale = null)
    {
        var s = AccountLocale.For(locale ?? "en");
        var account = ModelState.IsValid ? await users.FindByIdAsync(input.UserId) : null;
        var success = account is not null && !account.EmailConfirmed && (await users.ConfirmEmailAsync(account, input.Token)).Succeeded;
        return InfoPage(s.EmailConfirmed,
            message: success ? s.EmailConfirmedMessage : s.EmailConfirmedError,
            isError: !success,
            linksHtml: $"<a href=\"{BffLogin}\">{s.SignIn}</a>");
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string returnUrl = "/", [FromQuery] string? locale = null)
    {
        if (!Url.IsLocalUrl(returnUrl)) return BadRequest();
        locale ??= ExtractUiLocales(returnUrl);
        var s = AccountLocale.For(locale ?? "en");
        return Page(s.SignIn, [("Email", "email", ""), ("Password", "password", ""), ("ReturnUrl", "hidden", returnUrl)],
            linksHtml: $"{s.NoAccount} <a href=\"/account/register{LocaleQuery(locale)}\">{s.CreateLink}</a><br>{s.ForgotPassword} <a href=\"/account/forgot{LocaleQuery(locale)}\">{s.ResetLink}</a>");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginInput input, [FromQuery] string? locale = null)
    {
        if (!Url.IsLocalUrl(input.ReturnUrl)) return BadRequest();
        locale ??= ExtractUiLocales(input.ReturnUrl);
        var s = AccountLocale.For(locale ?? "en");
        var account = ModelState.IsValid ? await users.FindByEmailAsync(input.Email.Trim()) : null;
        if (account is not null && (await signIn.PasswordSignInAsync(account, input.Password, false, true)).Succeeded)
        {
            var path = input.ReturnUrl.Split('?', 2)[0];
            var query = QueryHelpers.ParseQuery(new Uri("https://local.test" + input.ReturnUrl).Query)
                .Where(pair => pair.Key is not ("prompt" or "max_age"));
            var destination = QueryHelpers.AddQueryString(path, query);
            return LocalRedirect(destination);
        }

        return Page(s.SignIn, [("Email", "email", input.Email), ("Password", "password", ""), ("ReturnUrl", "hidden", input.ReturnUrl)],
            message: s.SignInFailed, isError: true,
            linksHtml: $"{s.NoAccount} <a href=\"/account/register{LocaleQuery(locale)}\">{s.CreateLink}</a><br>{s.ForgotPassword} <a href=\"/account/forgot{LocaleQuery(locale)}\">{s.ResetLink}</a>");
    }

    [HttpGet("forgot")]
    public IActionResult Forgot([FromQuery] string? locale = null)
    {
        var s = AccountLocale.For(locale ?? "en");
        return Page(s.ResetPassword, [("Email", "email", "")],
            sub: s.ResetPasswordSub,
            linksHtml: $"{s.Remembered} <a href=\"/account/login{LocaleQuery(locale)}\">{s.SignInLink}</a>");
    }

    [HttpPost("forgot")]
    public async Task<IActionResult> Forgot([FromForm] RecoveryInput input, [FromQuery] string? locale, CancellationToken cancellationToken)
    {
        var s = AccountLocale.For(locale ?? "en");
        var account = ModelState.IsValid ? await users.FindByEmailAsync(input.Email.Trim()) : null;
        if (account is { EmailConfirmed: true })
        {
            await emailSender.SendAsync(account.Email!, "Reset your OurSpace password",
                Link("/account/reset", account.Id, await users.GeneratePasswordResetTokenAsync(account)), cancellationToken);
        }

        return InfoPage(s.CheckEmail,
            message: s.CheckEmailMessage,
            linksHtml: $"{s.BackToSignIn} <a href=\"{BffLogin}\">{s.SignInLink}</a>");
    }

    [HttpGet("reset")]
    public IActionResult Reset([FromQuery] string userId, [FromQuery] string token, [FromQuery] string? locale = null)
    {
        var s = AccountLocale.For(locale ?? "en");
        return Page(s.ChooseNewPassword, [("UserId", "hidden", userId), ("Token", "hidden", token), ("Password", "password", "")],
            sub: s.ChooseNewPasswordSub);
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset([FromForm] TokenInput input, [FromQuery] string? locale = null)
    {
        var s = AccountLocale.For(locale ?? "en");
        var account = ModelState.IsValid && input.Password is { Length: >= 12 and <= 128 } ? await users.FindByIdAsync(input.UserId) : null;
        var success = account is not null && account.EmailConfirmed && (await users.ResetPasswordAsync(account, input.Token, input.Password!)).Succeeded;
        if (success) await authorizations.RevokeBySubjectAsync(account!.Id.ToString());
        return InfoPage(s.PasswordReset,
            message: success ? s.PasswordChanged : s.PasswordResetError,
            isError: !success,
            linksHtml: $"<a href=\"{BffLogin}\">{s.SignInLink}</a>");
    }

    private string BffLogin => settings.Value.BffOrigin + "/bff/login";

    // Extracts ui_locales from a returnUrl like /connect/authorize?...&ui_locales=ro...
    private static string? ExtractUiLocales(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl) || !returnUrl.Contains('?')) return null;
        var inner = QueryHelpers.ParseQuery(returnUrl[(returnUrl.IndexOf('?'))..]);
        return inner.TryGetValue("ui_locales", out var v) ? v.ToString().Split(' ')[0] : null;
    }

    private static string LocaleQuery(string? locale) =>
        string.IsNullOrEmpty(locale) || locale == "en" ? string.Empty : "?locale=" + locale;

    private string Link(string path, Guid userId, string token) =>
        QueryHelpers.AddQueryString(settings.Value.Issuer.TrimEnd('/') + path,
            new Dictionary<string, string?> { ["userId"] = userId.ToString(), ["token"] = token });

    private ContentResult Page(string title, IEnumerable<(string Name, string Type, string Value)> fields,
        string? message = null, bool isError = false, string? sub = null, string? linksHtml = null) =>
        Content(AccountPage.Form(HttpContext, antiforgery, title, fields, message, isError, sub, linksHtml), "text/html; charset=utf-8");

    private ContentResult InfoPage(string title, string? message = null, bool isError = false,
        string? sub = null, string? linksHtml = null) =>
        Content(AccountPage.Info(title, message, isError, sub, linksHtml), "text/html; charset=utf-8");
}
