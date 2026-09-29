namespace AuthServer.Infrastructure;

public static class AuthScopes
{
    // 開發者後台 API（developer-api）專用範疇，只授權給 developer-web，避免第三方應用程式以會員的 Token 存取開發者資產。
    public const string DeveloperApi = "developer_api";
}
