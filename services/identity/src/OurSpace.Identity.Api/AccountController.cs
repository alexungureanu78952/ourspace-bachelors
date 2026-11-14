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
    private const string Sent = "Check your inbox — if the account is eligible an email is on its way.";

    [HttpGet("register")]
    public IActionResult Register() =>
        Page("Create account", [("Email", "email", ""), ("Password", "password", "")],
            sub: "Pick an email and a password of 12–128 characters.",
            linksHtml: "Already have an account? <a href=\"/account/login\">Sign in</a>");

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] RegisterInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return Page("Create account", [("Email", "email", ""), ("Password", "password", "")],
                message: "Enter a valid email and a password of 12–128 characters.", isError: true,
                linksHtml: "Already have an account? <a href=\"/account/login\">Sign in</a>");

        var address = input.Email.Trim();
        var candidate = new IdentityUserAccount { Id = Guid.NewGuid(), UserName = address, Email = address };
        foreach (var validator in users.PasswordValidators)
        {
            if (!(await validator.ValidateAsync(users, candidate, input.Password)).Succeeded)
                return Page("Create account", [("Email", "email", ""), ("Password", "password", "")],
                    message: "Use 12–128 characters with upper/lowercase letters, a number and a symbol.", isError: true,
                    linksHtml: "Already have an account? <a href=\"/account/login\">Sign in</a>");
        }

        var account = await users.FindByEmailAsync(address);
        if (account is null && (await users.CreateAsync(candidate, input.Password)).Succeeded) account = candidate;
        if (account is { EmailConfirmed: false } && await users.CheckPasswordAsync(account, input.Password))
        {
            var token = await users.GenerateEmailConfirmationTokenAsync(account);
            await emailSender.SendAsync(address, "Confirm your OurSpace email",
                Link("/account/confirm", account.Id, token), cancellationToken);
        }

        return Page("Check your email", [],
            message: Sent,
            linksHtml: "Back to <a href=\"/account/login\">sign in</a>");
    }

    [HttpGet("confirm")]
    public IActionResult Confirm([FromQuery] string userId, [FromQuery] string token) =>
        Page("Confirm email", [("UserId", "hidden", userId), ("Token", "hidden", token)],
            sub: "Click Continue to confirm your email address.");

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromForm] TokenInput input)
    {
        var account = ModelState.IsValid ? await users.FindByIdAsync(input.UserId) : null;
        var success = account is not null && !account.EmailConfirmed && (await users.ConfirmEmailAsync(account, input.Token)).Succeeded;
        return Page("Email confirmed", [],
            message: success ? "Email confirmed. You can now sign in." : "This confirmation link is invalid or has already been used.",
            isError: !success,
            linksHtml: "<a href=\"/account/login\">Sign in</a>");
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl)) return BadRequest();
        return Page("Sign in", [("Email", "email", ""), ("Password", "password", ""), ("ReturnUrl", "hidden", returnUrl)],
            linksHtml: "No account yet? <a href=\"/account/register\">Create one</a><br>Forgot your password? <a href=\"/account/forgot\">Reset it</a>");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginInput input)
    {
        if (!Url.IsLocalUrl(input.ReturnUrl)) return BadRequest();
        var account = ModelState.IsValid ? await users.FindByEmailAsync(input.Email.Trim()) : null;
        if (account is not null && (await signIn.PasswordSignInAsync(account, input.Password, false, true)).Succeeded)
        {
            var path = input.ReturnUrl.Split('?', 2)[0];
            var query = QueryHelpers.ParseQuery(new Uri("https://local.test" + input.ReturnUrl).Query)
                .Where(pair => pair.Key is not ("prompt" or "max_age"));
            var destination = QueryHelpers.AddQueryString(path, query);
            return LocalRedirect(destination);
        }

        return Page("Sign in", [("Email", "email", input.Email), ("Password", "password", ""), ("ReturnUrl", "hidden", input.ReturnUrl)],
            message: "Sign-in failed. Check your credentials or confirm your email first.", isError: true,
            linksHtml: "No account yet? <a href=\"/account/register\">Create one</a><br>Forgot your password? <a href=\"/account/forgot\">Reset it</a>");
    }

    [HttpGet("forgot")]
    public IActionResult Forgot() =>
        Page("Reset password", [("Email", "email", "")],
            sub: "Enter your email and we'll send a reset link.",
            linksHtml: "Remembered it? <a href=\"/account/login\">Sign in</a>");

    [HttpPost("forgot")]
    public async Task<IActionResult> Forgot([FromForm] RecoveryInput input, CancellationToken cancellationToken)
    {
        var account = ModelState.IsValid ? await users.FindByEmailAsync(input.Email.Trim()) : null;
        if (account is { EmailConfirmed: true })
        {
            await emailSender.SendAsync(account.Email!, "Reset your OurSpace password",
                Link("/account/reset", account.Id, await users.GeneratePasswordResetTokenAsync(account)), cancellationToken);
        }

        return Page("Check your email", [],
            message: Sent,
            linksHtml: "Back to <a href=\"/account/login\">sign in</a>");
    }

    [HttpGet("reset")]
    public IActionResult Reset([FromQuery] string userId, [FromQuery] string token) =>
        Page("Choose a new password", [("UserId", "hidden", userId), ("Token", "hidden", token), ("Password", "password", "")],
            sub: "Pick a new password of 12–128 characters.");

    [HttpPost("reset")]
    public async Task<IActionResult> Reset([FromForm] TokenInput input)
    {
        var account = ModelState.IsValid && input.Password is { Length: >= 12 and <= 128 } ? await users.FindByIdAsync(input.UserId) : null;
        var success = account is not null && account.EmailConfirmed && (await users.ResetPasswordAsync(account, input.Token, input.Password!)).Succeeded;
        if (success) await authorizations.RevokeBySubjectAsync(account!.Id.ToString());
        return Page("Password reset", [],
            message: success ? "Password changed. You can now sign in." : "This reset request is invalid.",
            isError: !success,
            linksHtml: "<a href=\"/account/login\">Sign in</a>");
    }

    private string Link(string path, Guid userId, string token) =>
        QueryHelpers.AddQueryString(settings.Value.Issuer.TrimEnd('/') + path,
            new Dictionary<string, string?> { ["userId"] = userId.ToString(), ["token"] = token });

    private ContentResult Page(string title, IEnumerable<(string Name, string Type, string Value)> fields,
        string? message = null, bool isError = false, string? sub = null, string? linksHtml = null) =>
        Content(AccountPage.Form(HttpContext, antiforgery, title, fields, message, isError, sub, linksHtml), "text/html; charset=utf-8");
}
