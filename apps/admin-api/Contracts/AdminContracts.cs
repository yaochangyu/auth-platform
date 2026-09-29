using System.Text.Json;
using AdminApi.Entities;
using AuthShared;

namespace AdminApi.Contracts;

public record AdminApplicationResponse(
    Guid Id,
    Guid OwnerMemberId,
    string ClientId,
    string Name,
    string Description,
    string ContactEmail,
    ApplicationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static AdminApplicationResponse From(ManagedApplication application) => new(
        application.Id,
        application.OwnerMemberId,
        application.ClientId,
        application.Name,
        application.Description,
        application.ContactEmail,
        application.Status,
        application.CreatedAt,
        application.UpdatedAt);
}

public record AdminApplicationListResponse(IReadOnlyList<AdminApplicationResponse> Items, int Total, int Page, int PageSize);

public record StatusChangeRequest(ApplicationStatus Status, string? Reason);

// 斷路連帶作廢的數量，同時寫進稽核紀錄的補充資訊。
public record CircuitBreakerResult(int RevokedApiKeys, long RevokedAuthorizations, long RevokedTokens);

public record StatusChangeResponse(AdminApplicationResponse Application, CircuitBreakerResult? CircuitBreaker);

public record AuditLogResponse(
    long Id,
    DateTimeOffset OccurredAt,
    Guid ActorMemberId,
    string ClientIp,
    string Action,
    string TargetType,
    string TargetId,
    JsonElement? Before,
    JsonElement? After,
    JsonElement? Details)
{
    public static AuditLogResponse From(AuditLog log) => new(
        log.Id,
        log.OccurredAt,
        log.ActorMemberId,
        log.ClientIp,
        log.Action,
        log.TargetType,
        log.TargetId,
        Parse(log.BeforeJson),
        Parse(log.AfterJson),
        Parse(log.DetailsJson));

    private static JsonElement? Parse(string? json) => json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);
}

public record AuditLogListResponse(IReadOnlyList<AuditLogResponse> Items, int Total, int Page, int PageSize);
