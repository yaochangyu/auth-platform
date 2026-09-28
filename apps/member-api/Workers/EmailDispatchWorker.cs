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

        var pendingMessages = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAt == null)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var message in pendingMessages)
        {
            // ponytail: 未串接真實 SMTP/SES/SendGrid，僅記錄並標記已處理；上線前補上實際寄信整合
            logger.LogInformation("派發驗證信予 {ToEmail}：{Subject}", message.ToEmail, message.Subject);
            message.ProcessedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
