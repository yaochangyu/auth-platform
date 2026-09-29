namespace MemberApi.Entities;

// 會員對某個 Connected App 的授權關係。RevokedAt 為 null 代表目前仍有效；
// 撤銷採軟刪除（保留紀錄）而非實體刪除列，保留未來稽核需求的彈性。
public class MemberGrant
{
    public Guid Id { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public Guid ConnectedAppId { get; set; }

    public ConnectedApp? ConnectedApp { get; set; }

    public required string[] Scopes { get; set; }

    public DateTimeOffset AuthorizedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
