using MailKit.Security;
using MimeKit;

namespace OurSpace.Identity.Api;

public interface IAccountEmailSender
{
    Task SendAsync(string recipient, string subject, string link, CancellationToken cancellationToken);
}

public sealed class SmtpAccountEmailSender(IConfiguration configuration, IHostEnvironment environment) : IAccountEmailSender
{
    public async Task SendAsync(string recipient, string subject, string link, CancellationToken cancellationToken)
    {
        var host = configuration["Email:Host"] ?? throw new InvalidOperationException("Email:Host is required.");
        var useTls = configuration.GetValue("Email:UseTls", true);
        var checkRevocation = configuration.GetValue("Email:CheckCertificateRevocation", true);
        if (!checkRevocation && !environment.IsEnvironment("LocalDocker"))
            throw new InvalidOperationException("Disabling SMTP revocation checks is permitted only in LocalDocker.");
        if (!useTls && !environment.IsDevelopment())
        {
            throw new InvalidOperationException("Unencrypted SMTP is permitted only in Development.");
        }

        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(configuration["Email:From"] ?? "accounts@ourspace.test"));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = "Use this link to continue:\n" + link };
        using var smtp = new MailKit.Net.Smtp.SmtpClient { Timeout = 10000, CheckCertificateRevocation = checkRevocation };
        await smtp.ConnectAsync(host, configuration.GetValue("Email:Port", 587),
            useTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
        if (configuration["Email:Username"] is { Length: > 0 } username)
        {
            await smtp.AuthenticateAsync(username, configuration["Email:Password"] ?? throw new InvalidOperationException("Email:Password is required."), cancellationToken);
        }

        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }
}
