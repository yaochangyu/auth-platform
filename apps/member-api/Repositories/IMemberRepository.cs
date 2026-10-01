using MemberApi.Domain;
using MemberApi.Entities;

namespace MemberApi.Repositories;

public interface IMemberRepository
{
    Task<Member?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<Member?> FindByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);

    Task<Member?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<VerificationToken?> FindVerificationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<List<VerificationToken>> FindActiveVerificationTokensAsync(Guid memberId, VerificationTokenPurpose purpose, CancellationToken cancellationToken);

    Task<VerificationToken?> FindLatestVerificationTokenAsync(Guid memberId, VerificationTokenPurpose purpose, CancellationToken cancellationToken);

    void AddMember(Member member);

    void AddVerificationToken(VerificationToken verificationToken);

    void AddOutboxMessage(OutboxMessage outboxMessage);

    /// <summary>
    /// 鎖定列後以 LoginLockoutPolicy 計算並寫回登入失敗狀態（列鎖需要交易），回傳新狀態；會員不存在回傳 null。
    /// 並行的密碼錯誤因列鎖而序列化，計數不會 Lost Update。
    /// </summary>
    Task<LoginLockoutState?> RegisterFailedLoginAsync(Guid memberId, DateTimeOffset now, CancellationToken cancellationToken);

    Task RevokeAllTokensForMemberAsync(Guid memberId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
