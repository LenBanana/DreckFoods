using FoodDbAPI.Models.Settings;
using FoodDbAPI.Services.Interfaces;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FoodDbAPI.Services;

public class EmailSender(IOptions<SmtpSettings> smtpOpt, ILogger<EmailSender> logger)
    : IEmailSender
{
    private readonly SmtpSettings _smtp = smtpOpt.Value;

    public async Task SendEmailAsync(string to, string subject, string htmlMessage)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.SenderName, _smtp.SenderEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(_smtp.Host, _smtp.Port, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_smtp.Username, _smtp.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        logger.LogInformation("Sent email '{Subject}' to {To}", subject, to);
    }
}