namespace AuthServer.Infrastructure;

public record MemberSnapshot(
    string SecurityStamp,
    string Role,
    string Email,
    string DisplayName,
    bool EmailVerified,
    DateTimeOffset UpdatedAt);

// auth-server 讀取會員資料的唯一 Seam：換票（Security Stamp、Role）與 UserInfo（Email、DisplayName…）都走這裡。
public interface IMemberDirectory
{
    Task<MemberSnapshot?> GetAsync(Guid memberId, CancellationToken ct = default);
}
