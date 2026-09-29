namespace AuthShared;

// OpenIddict 應用程式（OAuth Client）的 Properties 內使用的鍵。
public static class ClientProperties
{
    // 值為 true 時代表該 Client 已被管理員停用（斷路）：auth-server 拒絕它的所有請求，Client 設定原樣保留，取消停用即可還原。
    public const string Suspended = "suspended";
}
