using System.Security.Cryptography;

namespace MemberApi.Security;

public static class VerificationTokenGenerator
{
    public static string Generate()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}
