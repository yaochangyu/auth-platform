namespace AdminApi;

public static class AuthPolicies
{
    public const string Admin = "Admin";

    // 只有 auth-server 授權給 admin-web 的 Token 帶有此範疇；第三方應用程式取得的會員 Token 不能存取管理功能。
    public const string AdminApiScope = "admin_api";

    public const string AdminRole = "admin";
}
