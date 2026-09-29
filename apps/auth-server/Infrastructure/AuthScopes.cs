namespace AuthServer.Infrastructure;

public static class AuthScopes
{
    // 開發者後台 API（developer-api）專用範疇，只授權給 developer-web，避免第三方應用程式以會員的 Token 存取開發者資產。
    public const string DeveloperApi = "developer_api";

    // 管理後台 API（admin-api）專用範疇，只授權給 admin-web。帶有此範疇的 Access Token 才會附上會員的平台角色（role claim），
    // 第三方應用程式因此看不到會員是不是管理員。
    public const string AdminApi = "admin_api";
}
