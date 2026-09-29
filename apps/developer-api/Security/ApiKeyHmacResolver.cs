using System.Security.Claims;
using System.Security.Cryptography;
using AuthShared.Hmac;
using DeveloperApi.Entities;
using DeveloperApi.Repositories;

namespace DeveloperApi.Security;

// 以 API Key 找出對應的 API Secret 與身分，交給 AuthShared 的 HMAC 驗證處理常式驗簽。
public class ApiKeyHmacResolver(ApiKeyRepository repository, TimeProvider timeProvider, ILogger<ApiKeyHmacResolver> logger) : IHmacCredentialResolver
{
    public async Task<HmacCredential?> ResolveAsync(string apiKey, CancellationToken cancellationToken)
    {
        (ApiKey Key, string ClientId, string Secret)? found;
        try
        {
            found = await repository.FindActiveByApiKeyAsync(apiKey, timeProvider.GetUtcNow(), cancellationToken);
        }
        catch (CryptographicException ex)
        {
            // Data Protection 金鑰環遺失或不一致時，所有 API Secret 都解不開：對呼叫端視為驗證失敗（401），
            // 但要留下錯誤日誌讓維運發現，而不是每個請求都回 500。
            logger.LogError(ex, "無法解密 API Secret，請檢查 Data Protection 金鑰環（Auth:DataProtectionKeyDirectory）是否遺失或不一致。");
            return null;
        }

        if (found is not var (key, clientId, secret))
        {
            return null;
        }

        return new HmacCredential(secret,
        [
            new Claim("client_id", clientId),
            new Claim("api_key_id", key.Id.ToString()),
            new Claim("scope", string.Join(' ', key.Scopes)),
        ]);
    }
}
