namespace MemberApi.Email;

// ponytail: 尚未串接真實 SMTP/SES/SendGrid，僅記錄寄信內容供開發/測試觀察；
// 上線前依實際供應商實作 IEmailSender，Worker 端的重試/退避邏輯不需變動。
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("寄送信件至 {ToEmail}：{Subject}", toEmail, subject);
        return Task.CompletedTask;
    }
}
