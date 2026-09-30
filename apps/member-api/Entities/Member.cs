namespace MemberApi.Entities;

public class Member
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public required string DisplayName { get; set; }

    public MemberStatus Status { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTimeOffset? LockoutEndAt { get; set; }

    // ADR-0002：密碼變更/重設時刷新，Cookie 驗證中介層比對此值以立即註銷舊裝置的歷史 Session
    public required string SecurityStamp { get; set; }

    // 平台角色：一般會員為 member，平台管理員為 admin。沒有自助升級管道，由營運直接更新資料庫指定。
    // auth-server 在授權範疇含 admin_api 時，於換票當下讀取並寫入 Access Token 的 role claim。
    public string Role { get; set; } = "member";

    public DateTimeOffset? EmailVerifiedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    // Issue #25: 增量屬性補填
    public DateOnly? Birthday { get; set; }

    public string? Education { get; set; }

    public string? Address { get; set; }

    public string? JobTitle { get; set; }

    // Issue #21 / #35: 手機號碼與簡訊驗證狀態
    public string? PhoneNumber { get; set; }

    public DateTimeOffset? PhoneVerifiedAt { get; set; }
}
