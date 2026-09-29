using DeveloperApi.Entities;

namespace DeveloperApi.Contracts;

public record ApiKeyRequest(string Name, ApiKeyEnvironment Environment, IReadOnlyList<string> Scopes, DateTimeOffset? ExpiresAt);

public record ApiKeySummary(
    Guid Id,
    string Name,
    ApiKeyEnvironment Environment,
    string Prefix,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    string Status)
{
    public static ApiKeySummary From(ApiKey key, DateTimeOffset now) => new(
        key.Id,
        key.Name,
        key.Environment,
        key.Prefix,
        key.Scopes,
        key.CreatedAt,
        key.ExpiresAt,
        key.RevokedAt,
        key.RevokedAt is not null ? "Revoked" : key.IsActive(now) ? "Active" : "Expired");
}

public record ApiKeyListResponse(IReadOnlyList<ApiKeySummary> Items, IReadOnlyList<string> AllowedScopes);

// API Key 與 API Secret 明文只在發行當下回傳一次。
public record IssuedApiKeyResponse(
    Guid Id,
    string ApiKey,
    string ApiSecret,
    string Prefix,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);

public record M2mEchoResponse(string ClientId, Guid ApiKeyId, IReadOnlyList<string> Scopes, int BodyLength);
