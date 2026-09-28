using System.Security.Cryptography;
using System.Text;

namespace MemberApi.Security;

public static class VerificationTokenHasher
{
    public static string Hash(string rawToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
