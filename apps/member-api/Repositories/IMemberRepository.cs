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
    /// 鎖定列後讀出目前的鎖定狀態，以 <paramref name="transition"/> 計算新狀態並寫回（同一交易），回傳新狀態；會員不存在回傳 null。
    /// 並行的密碼錯誤因列鎖而序列化，計數不會 Lost Update。規則本身在 LoginLockoutPolicy，這裡只負責存取。
    /// </summary>
    Task<LoginLockoutState?> UpdateLockoutAsync(
        Guid memberId,
        Func<LoginLockoutState, LoginLockoutState> transition,
        CancellationToken cancellationToken);

    Task RevokeAllTokensForMemberAsync(Guid memberId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
