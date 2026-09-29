using AuthShared;
using System.Security.Cryptography;
using System.Text;
using DeveloperApi.Entities;
using DeveloperApi.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace DeveloperApi.Repositories;

public record NewApiKey(ApiKey Entity, string ApiKey, string ApiSecret);

public class ApiKeyRepository(DeveloperApiDbContext dbContext, IDataProtectionProvider dataProtection)
{
    // 可綁定的範疇。目前平台尚無以 M2M 存取的業務資源，先開放會員資料相關的 profile、email；
    // 有具體的資源 API 後再擴充。openid、offline_access 只對會員身分有意義，developer_api 為內部專用。
    public static readonly IReadOnlyList<string> AllowedScopes = ["profile", "email"];

    private readonly IDataProtector _secretProtector = dataProtection.CreateProtector("DeveloperApi.ApiKeySecret");

    public Task<List<ApiKey>> ListAsync(Guid applicationId, CancellationToken cancellationToken) =>
        dbContext.ApiKeys.AsNoTracking().Where(key => key.ApplicationId == applicationId).OrderByDescending(key => key.CreatedAt).ToListAsync(cancellationToken);

    public Task<ApiKey?> FindAsync(Guid applicationId, Guid keyId, CancellationToken cancellationToken) =>
        dbContext.ApiKeys.SingleOrDefaultAsync(key => key.Id == keyId && key.ApplicationId == applicationId, cancellationToken);

    public async Task<NewApiKey> CreateAsync(
        Guid applicationId, string name, ApiKeyEnvironment environment, IReadOnlyList<string> scopes, DateTimeOffset now, DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        var envPrefix = environment == ApiKeyEnvironment.Live ? "ak_live_" : "ak_test_";
        var apiKey = envPrefix + RandomToken();
        var apiSecret = "as_" + RandomToken();

        var entity = new ApiKey
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            Name = name,
            Environment = environment,
            Prefix = apiKey[..(envPrefix.Length + 8)],
            KeyHash = Hash(apiKey),
            SecretProtected = this._secretProtector.Protect(apiSecret),
            Scopes = [.. scopes],
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };
        dbContext.ApiKeys.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new NewApiKey(entity, apiKey, apiSecret);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    // 驗證用：以 API Key 找出仍有效（未撤銷、未過期）且所屬專案為 Active 的金鑰，並還原 API Secret。
    public async Task<(ApiKey Key, string ClientId, string Secret)?> FindActiveByApiKeyAsync(string apiKey, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var hash = Hash(apiKey);
        var row = await (
            from key in dbContext.ApiKeys.AsNoTracking()
            join application in dbContext.Applications.AsNoTracking() on key.ApplicationId equals application.Id
            where key.KeyHash == hash && application.Status == ApplicationStatus.Active
            select new { Key = key, application.ClientId }).SingleOrDefaultAsync(cancellationToken);

        return row is { } found && found.Key.IsActive(now)
            ? (found.Key, found.ClientId, this._secretProtector.Unprotect(found.Key.SecretProtected))
            : null;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string RandomToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
