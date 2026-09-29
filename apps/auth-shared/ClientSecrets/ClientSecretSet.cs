using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AuthShared.ClientSecrets;

/// <summary>
/// 一個 OAuth Client 的 Secret 集合，支援雙金鑰平滑輪替（Graceful Rotation）：
/// 發行新 Secret 時，既有有效的 Secret 進入過渡期（到期前仍可用），到期或被作廢後立即失效。
/// 集合序列化後存放於 OpenIddict 應用程式的 <see cref="PropertyName"/> 屬性，由 developer-api 寫入、auth-server 驗證。
/// </summary>
public sealed class ClientSecretSet(IReadOnlyList<ClientSecretEntry> entries)
{
    public const string PropertyName = "client_secrets";

    // 同時有效的 Secret 上限：目前使用中的一組加上過渡期中的一組。
    public const int MaxActive = 2;

    public static ClientSecretSet Empty { get; } = new([]);

    public IReadOnlyList<ClientSecretEntry> Entries => entries;

    public static ClientSecretSet FromJson(JsonElement json) =>
        new(json.Deserialize<List<ClientSecretEntry>>() ?? []);

    public JsonElement ToJson() => JsonSerializer.SerializeToElement(entries);

    public int ActiveCount(DateTimeOffset now) => entries.Count(entry => entry.IsActive(now));

    public bool IsValid(string secret, DateTimeOffset now)
    {
        var candidate = HashBytes(secret);
        var valid = false;

        // 不提前跳出，讓比對時間不隨符合的是第幾組而變。
        foreach (var entry in entries.Where(entry => entry.IsActive(now)))
        {
            valid |= MatchesHash(candidate, entry.Hash);
        }

        return valid;
    }

    public (ClientSecretSet Set, string Plaintext, ClientSecretEntry Entry) Issue(DateTimeOffset now, TimeSpan gracePeriod)
    {
        if (this.ActiveCount(now) >= MaxActive)
        {
            throw new InvalidOperationException($"同時有效的 Secret 已達上限 {MaxActive} 組。");
        }

        var plaintext = "cs_" + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var entry = new ClientSecretEntry(
            Guid.NewGuid(), Convert.ToHexString(HashBytes(plaintext)), plaintext[..7], now, ExpiresAt: null, RevokedAt: null);

        // 既有有效的 Secret 進入過渡期；本來就更早到期的不延長。
        var graceEnd = now + gracePeriod;
        var existing = entries.Select(current =>
            current.IsActive(now) && (current.ExpiresAt is null || current.ExpiresAt > graceEnd)
                ? current with { ExpiresAt = graceEnd }
                : current);

        return (new ClientSecretSet([.. existing, entry]), plaintext, entry);
    }

    // 手動作廢：立即失效，不論是否仍在過渡期。找不到或已作廢時回傳 null。
    public ClientSecretSet? Revoke(Guid secretId, DateTimeOffset now)
    {
        if (!entries.Any(entry => entry.Id == secretId && entry.RevokedAt is null))
        {
            return null;
        }

        return new ClientSecretSet([.. entries.Select(entry => entry.Id == secretId ? entry with { RevokedAt = now } : entry)]);
    }

    // 資料被手動改壞（非 16 進位）時視為不符，而不是丟出例外變成 500；驗證一律 fail-closed。
    private static bool MatchesHash(byte[] candidate, string storedHex) =>
        Convert.FromHexString(storedHex.Length % 2 == 0 && storedHex.All(Uri.IsHexDigit) ? storedHex : "") is { Length: > 0 } stored
        && CryptographicOperations.FixedTimeEquals(candidate, stored);

    private static byte[] HashBytes(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
