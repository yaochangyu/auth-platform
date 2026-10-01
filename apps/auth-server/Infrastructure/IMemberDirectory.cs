namespace AuthServer.Infrastructure;

public record MemberSnapshot(
    string SecurityStamp,
    string Role,
    string Email,
    string DisplayName,
    bool EmailVerified,
    DateTimeOffset UpdatedAt)
{
    // Security Stamp 只存在 Session Cookie 與 Authorization Code / Refresh Token 內，不放任何 JWT。
    public const string SecurityStampClaim = "security_stamp";
}

// auth-server 讀取會員資料的唯一 Seam：換票（Security Stamp、Role）與 UserInfo（Email、DisplayName…）都走這裡。
public interface IMemberDirectory
{
    Task<MemberSnapshot?> GetAsync(Guid memberId, CancellationToken ct = default);
}
