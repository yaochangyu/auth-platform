namespace MemberApi.Security;

public static class RateLimitingPolicies
{
    public const string PublicAuth = "PublicAuthSlidingWindow";
    public const string Login = "LoginSlidingWindow";
}
