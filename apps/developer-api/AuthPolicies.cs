namespace DeveloperApi;

public static class AuthPolicies
{
    public const string DeveloperApi = "DeveloperApi";

    // 只有 auth-server 授權給 developer-web 的 Token 帶有此範疇；第三方應用程式取得的會員 Token 不能存取開發者資產。
    public const string M2mProfile = "M2mProfile";

    public const string DeveloperApiScope = "developer_api";
}
