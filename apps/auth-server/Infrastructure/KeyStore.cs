using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace AuthServer.Infrastructure;

public static class KeyStore
{
    // ponytail: 私鑰以 PEM 落地於磁碟（僅擁有者可讀）；多實例同時首次啟動可能各自產生金鑰，
    // 需輪替、多實例或 Vault/KMS 注入時再換實作。
    public static RsaSecurityKey LoadOrCreateSigningKey(string directory)
    {
        var path = Path.Combine(directory, "auth-signing-key.pem");
        var rsa = RSA.Create(2048);

        if (File.Exists(path))
        {
            rsa.ImportFromPem(File.ReadAllText(path));
        }
        else
        {
            Directory.CreateDirectory(directory);
            WriteAtomic(path, System.Text.Encoding.ASCII.GetBytes(rsa.ExportPkcs8PrivateKeyPem()));
        }

        var key = new RsaSecurityKey(rsa);
        key.KeyId = Base64UrlEncoder.Encode(key.ComputeJwkThumbprint());
        return key;
    }

    // 加密金鑰只用於 Authorization Code / Refresh Token 等內部票據，不會發布到 JWKS。
    public static SymmetricSecurityKey LoadOrCreateEncryptionKey(string directory)
    {
        var path = Path.Combine(directory, "auth-encryption.key");

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(directory);
            WriteAtomic(path, RandomNumberGenerator.GetBytes(32));
        }

        return new SymmetricSecurityKey(File.ReadAllBytes(path));
    }

    // 先寫暫存檔再改名，避免當機留下殘缺金鑰檔，下次啟動無法載入。
    private static void WriteAtomic(string path, byte[] content)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, content);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(temp, path, overwrite: true);
    }
}
