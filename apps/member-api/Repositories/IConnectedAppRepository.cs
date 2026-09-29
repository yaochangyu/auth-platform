using MemberApi.Entities;

namespace MemberApi.Repositories;

public interface IConnectedAppRepository
{
    Task<List<MemberGrant>> FindActiveGrantsAsync(Guid memberId, CancellationToken cancellationToken);

    /// <summary>
    /// 同時以 Grant Id 與擁有者 MemberId 查詢，查無或非本人持有一律回傳 null，
    /// 讓呼叫端統一回應 404，防止 IDOR（不洩漏其他會員是否持有該筆授權）。
    /// </summary>
    Task<MemberGrant?> FindActiveGrantAsync(Guid grantId, Guid memberId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
