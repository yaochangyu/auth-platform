using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MemberApi.Email;

public class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder();
        if (body.Contains("<html>", StringComparison.OrdinalIgnoreCase))
        {
            bodyBuilder.HtmlBody = body;
        }
        else
        {
            bodyBuilder.TextBody = body;
        }

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, _options.Security, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.UserName) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            logger.LogInformation("成功透過 SMTP ({Host}:{Port}) 發送信件至 {ToEmail}：{Subject}",
                _options.Host, _options.Port, toEmail, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "透過 SMTP ({Host}:{Port}) 發送信件至 {ToEmail} 失敗：{ErrorMessage}",
                _options.Host, _options.Port, toEmail, ex.Message);
            throw;
        }
    }
}
