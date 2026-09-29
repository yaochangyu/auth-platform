namespace DeveloperApi.Entities;

public enum ApiKeyEnvironment
{
    Live,
    Test,
}

public class ApiKey
{
    public Guid Id { get; set; }

    public Guid ApplicationId { get; set; }

    public required string Name { get; set; }

    public ApiKeyEnvironment Environment { get; set; }

    // 環境前綴加上金鑰前 8 碼，僅供辨識，無法用來還原金鑰。
    public required string Prefix { get; set; }

    // API Key 的 SHA-256 雜湊（16 進位）；驗證時以請求帶來的 API Key 雜湊後比對。
    public required string KeyHash { get; set; }

    // HMAC 簽章用的 API Secret：伺服器必須能取回明文才能重算簽章，所以無法只存雜湊，
    // 改以 Data Protection 加密保存（資料庫外洩但沒有金鑰環時無法還原）。
    public required string SecretProtected { get; set; }

    public required string[] Scopes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive(DateTimeOffset now) => this.RevokedAt is null && (this.ExpiresAt is null || this.ExpiresAt > now);
}
