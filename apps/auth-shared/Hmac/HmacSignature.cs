using System.Security.Cryptography;
using System.Text;

namespace AuthShared.Hmac;

/// <summary>
/// HMAC-SHA256 請求簽章（Server-to-Server 防竄改、防重放）。
/// 待簽字串以換行串接：<c>METHOD \n PATH_AND_QUERY \n TIMESTAMP \n SHA256_HEX(BODY)</c>，
/// 簽章為以 API Secret 對該字串做 HMAC-SHA256 後的小寫 16 進位。
/// </summary>
public static class HmacSignature
{
    public const string ApiKeyHeader = "X-Api-Key";
    public const string TimestampHeader = "X-Timestamp";
    public const string SignatureHeader = "X-Signature";

    public static string Canonicalize(string method, string pathAndQuery, long unixSeconds, ReadOnlySpan<byte> body) =>
        $"{method.ToUpperInvariant()}\n{pathAndQuery}\n{unixSeconds}\n{Convert.ToHexStringLower(SHA256.HashData(body))}";

    public static string Compute(string secret, string method, string pathAndQuery, long unixSeconds, ReadOnlySpan<byte> body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(Canonicalize(method, pathAndQuery, unixSeconds, body))));

    // 以固定時間比對，避免從比對耗時推測簽章內容；格式不正確的簽章一律視為不符。
    public static bool Verify(string secret, string providedSignature, string method, string pathAndQuery, long unixSeconds, ReadOnlySpan<byte> body)
    {
        var expected = Encoding.ASCII.GetBytes(Compute(secret, method, pathAndQuery, unixSeconds, body));
        var provided = Encoding.ASCII.GetBytes(providedSignature.ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }
}
