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

    public DateTimeOffset CreatedAt { get; set; }
}
