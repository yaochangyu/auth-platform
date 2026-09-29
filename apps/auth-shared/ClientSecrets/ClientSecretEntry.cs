namespace AuthShared.ClientSecrets;

/// <summary>單一 Client Secret 的保存內容：只有 SHA-256 雜湊，無法反推明文。</summary>
public record ClientSecretEntry(
    Guid Id,
    string Hash,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt)
{
    public bool IsActive(DateTimeOffset now) => this.RevokedAt is null && (this.ExpiresAt is null || this.ExpiresAt > now);
}
