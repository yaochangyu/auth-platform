using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AdminApi.Tests.Support;

// 模擬 auth-server 簽發的 Access Token：RS256、iss、sub、以空白分隔的 scope、管理後台換票時附上的 role。
public static class TestJwtIssuer
{
    public const string Issuer = "https://auth.test";

    public static RsaSecurityKey SigningKey { get; } = new(RSA.Create(2048)) { KeyId = "test-key" };

    public static string Create(string subject, string scope = "openid profile admin_api", string? role = "admin", TimeSpan? lifetime = null, RsaSecurityKey? signingKey = null)
    {
        var expires = DateTime.UtcNow + (lifetime ?? TimeSpan.FromMinutes(15));
        var claims = new Dictionary<string, object> { ["sub"] = subject, ["scope"] = scope };
        if (role is not null)
        {
            claims["role"] = role;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Claims = claims,
            NotBefore = expires.AddMinutes(-15),
            Expires = expires,
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }
}
