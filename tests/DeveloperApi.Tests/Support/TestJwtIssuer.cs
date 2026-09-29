using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DeveloperApi.Tests.Support;

// 模擬 auth-server 簽發的 Access Token：RS256、iss、sub、以空白分隔的 scope claim、無 aud。
public static class TestJwtIssuer
{
    public const string Issuer = "https://auth.test";

    public static RsaSecurityKey SigningKey { get; } = new(RSA.Create(2048)) { KeyId = "test-key" };

    public static string Create(
        Guid memberId,
        string scope = "openid profile developer_api",
        TimeSpan? lifetime = null,
        RsaSecurityKey? signingKey = null) =>
        CreateWithSubject(memberId.ToString(), scope, lifetime, signingKey);

    public static string CreateWithSubject(
        string subject,
        string scope = "openid profile developer_api",
        TimeSpan? lifetime = null,
        RsaSecurityKey? signingKey = null)
    {
        var expires = DateTime.UtcNow + (lifetime ?? TimeSpan.FromMinutes(15));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Claims = new Dictionary<string, object> { ["sub"] = subject, ["scope"] = scope },
            NotBefore = expires.AddMinutes(-15),
            Expires = expires,
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }
}
