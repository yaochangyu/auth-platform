namespace DeveloperApi.Contracts;

public enum OAuthClientType
{
    Public,
    Confidential,
}

public record OAuthClientRequest(
    OAuthClientType ClientType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes);

public record ClientSecretSummary(Guid Id, string Prefix, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt, string Status);

public record OAuthClientResponse(
    string ClientId,
    OAuthClientType ClientType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<ClientSecretSummary> Secrets);

// 明文只在發行當下回傳一次，之後無法再取得。
public record IssuedClientSecretResponse(Guid Id, string Secret, string Prefix, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);
