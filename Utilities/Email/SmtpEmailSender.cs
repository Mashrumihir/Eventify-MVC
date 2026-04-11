using System.Net;
using System.Net.Mail;
using Eventify.Models.Email;
using Microsoft.Extensions.Options;

namespace Eventify.Utilities.Email;

public class SmtpEmailSender(IOptions<SmtpEmailSettings> options) : IEmailSender
{
    private readonly SmtpEmailSettings _settings = options.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) ||
            string.IsNullOrWhiteSpace(_settings.Username) ||
            string.IsNullOrWhiteSpace(_settings.Password) ||
            string.IsNullOrWhiteSpace(_settings.FromEmail) ||
            _settings.Username.Contains("yourgmail", StringComparison.OrdinalIgnoreCase) ||
            _settings.Password.Contains("your-16-char", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password)
        };

        await client.SendMailAsync(message);
    }
}
