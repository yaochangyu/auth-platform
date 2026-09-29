using System.Text.Json;
using AdminApi.Contracts;
using AdminApi.Entities;
using AdminApi.Infrastructure;
using AuthShared;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace AdminApi.Services;

public enum StatusChangeOutcome
{
    Changed,
    NotFound,
    Unchanged,
}

// 應用專案審核狀態切換與緊急斷路器。所有動作與稽核紀錄在同一個資料庫交易內，任何一步失敗就全部回復。
public class ApplicationStatusService(
    AdminApiDbContext dbContext,
    IOpenIddictApplicationManager clients,
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens,
    TimeProvider timeProvider)
{
    public async Task<(StatusChangeOutcome Outcome, StatusChangeResponse? Result)> ChangeAsync(
        Guid actorMemberId, string clientIp, Guid applicationId, ApplicationStatus newStatus, string? reason, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // 先鎖定該專案的資料列直到交易結束：兩位管理員同時操作同一專案時，後到的會等前一個完成，
        // 再讀到已變更的狀態而回 Unchanged，不會重複斷路或讓 Client 標記與狀態不一致。
        await dbContext.Database.ExecuteSqlAsync($"select 1 from developer_applications where id = {applicationId} for update", cancellationToken);

        var application = await dbContext.Applications.SingleOrDefaultAsync(item => item.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return (StatusChangeOutcome.NotFound, null);
        }

        var before = application.Status;
        if (before == newStatus)
        {
            return (StatusChangeOutcome.Unchanged, null);
        }

        var now = timeProvider.GetUtcNow();
        CircuitBreakerResult? breaker = null;
        if (newStatus == ApplicationStatus.Suspended)
        {
            breaker = await this.TripAsync(application, now, cancellationToken);
        }
        else if (before == ApplicationStatus.Suspended)
        {
            // 取消停用只解除 Client 的停用標記；已撤銷的授權、Token 與 API Key 不會復原，需要使用者重新授權、開發者重新發行。
            await this.SetClientSuspendedAsync(application.ClientId, suspended: false, cancellationToken);
        }

        application.Status = newStatus;
        application.UpdatedAt = now;

        dbContext.AuditLogs.Add(new AuditLog
        {
            OccurredAt = now,
            ActorMemberId = actorMemberId,
            ClientIp = clientIp,
            Action = newStatus == ApplicationStatus.Suspended ? "application.suspend" : "application.activate",
            TargetType = "application",
            TargetId = application.Id.ToString(),
            BeforeJson = JsonSerializer.Serialize(new { status = before.ToString() }),
            AfterJson = JsonSerializer.Serialize(new { status = newStatus.ToString() }),
            DetailsJson = JsonSerializer.Serialize(new
            {
                reason,
                clientId = application.ClientId,
                revokedApiKeys = breaker?.RevokedApiKeys,
                revokedAuthorizations = breaker?.RevokedAuthorizations,
                revokedTokens = breaker?.RevokedTokens,
            }),
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (StatusChangeOutcome.Changed, new StatusChangeResponse(AdminApplicationResponse.From(application), breaker));
    }

    private async Task<CircuitBreakerResult> TripAsync(ManagedApplication application, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var revokedKeys = await dbContext.ApiKeys
            .Where(key => key.ApplicationId == application.Id && key.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(key => key.RevokedAt, now), cancellationToken);

        long revokedAuthorizations = 0;
        long revokedTokens = 0;
        if (await clients.FindByClientIdAsync(application.ClientId, cancellationToken) is { } client)
        {
            // 尚未設定 OAuth Client 的專案沒有授權與 Token 可撤銷。
            var clientId = (await clients.GetIdAsync(client, cancellationToken))!;
            revokedAuthorizations = await authorizations.RevokeByApplicationIdAsync(clientId, cancellationToken);
            revokedTokens = await tokens.RevokeByApplicationIdAsync(clientId, cancellationToken);
            await this.SetClientSuspendedAsync(client, suspended: true, cancellationToken);
        }

        return new CircuitBreakerResult(revokedKeys, revokedAuthorizations, revokedTokens);
    }

    private async Task SetClientSuspendedAsync(string clientId, bool suspended, CancellationToken cancellationToken)
    {
        if (await clients.FindByClientIdAsync(clientId, cancellationToken) is { } client)
        {
            await this.SetClientSuspendedAsync(client, suspended, cancellationToken);
        }
    }

    // 在 Client 的 Properties 標記停用，auth-server 據此拒絕它的所有請求；Client 設定原樣保留。
    private async Task SetClientSuspendedAsync(object client, bool suspended, CancellationToken cancellationToken)
    {
        var descriptor = new OpenIddictApplicationDescriptor();
        await clients.PopulateAsync(descriptor, client, cancellationToken);

        if (suspended)
        {
            descriptor.Properties[ClientProperties.Suspended] = JsonSerializer.SerializeToElement(true);
        }
        else
        {
            descriptor.Properties.Remove(ClientProperties.Suspended);
        }

        await clients.UpdateAsync(client, descriptor, cancellationToken);
    }
}
