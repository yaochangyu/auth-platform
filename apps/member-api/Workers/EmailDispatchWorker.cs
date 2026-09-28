using MemberApi.Email;
using MemberApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemberApi.Workers;

public class EmailDispatchWorker(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<EmailDispatchWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await this.DispatchPendingMessagesAsync(stoppingToken);
            await Task.Delay(PollInterval, timeProvider, stoppingToken);
        }
    }

    private async Task DispatchPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var now = timeProvider.GetUtcNow();
        var pendingMessages = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAt == null && message.RetryCount < message.MaxRetries)
            .ToListAsync(cancellationToken);

        foreach (var message in pendingMessages)
        {
            if (message.LastAttemptAt is not null && message.LastAttemptAt + Backoff(message.RetryCount) > now)
            {
                continue;
            }

            message.LastAttemptAt = now;
            try
            {
                await emailSender.SendAsync(message.ToEmail, message.Subject, message.Body, cancellationToken);
                message.ProcessedAt = now;
                message.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.ErrorMessage = ex.Message;
                logger.LogWarning(ex, "派發信件至 {ToEmail} 失敗，第 {RetryCount} 次重試", message.ToEmail, message.RetryCount);
            }
        }

        if (pendingMessages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static TimeSpan Backoff(int retryCount)
    {
        return TimeSpan.FromSeconds(Math.Pow(2, retryCount));
    }
}
