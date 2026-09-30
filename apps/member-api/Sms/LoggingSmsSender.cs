namespace MemberApi.Sms;

public class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("寄送簡訊至 {PhoneNumber}：{Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
