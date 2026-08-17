using DailyGourmet.Api.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DailyGourmet.Api.Services;

public class EmailService(IOptions<SmtpOptions> options, ILogger<EmailService> logger) : IEmailService
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string plainTextBody)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            logger.LogWarning("SMTP is not configured — skipping email to {ToEmail} ({Subject})", toEmail, subject);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = plainTextBody }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.SslOnConnect);
            if (!string.IsNullOrWhiteSpace(_options.Username))
                await client.AuthenticateAsync(_options.Username, _options.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // Never let email delivery failures break the caller's transaction.
            logger.LogError(ex, "Failed to send email to {ToEmail} ({Subject})", toEmail, subject);
        }
    }
}
