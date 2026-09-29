using MemberApi.Entities;

namespace MemberApi.Repositories;

public interface IMemberRepository
{
    Task<Member?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<Member?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<VerificationToken?> FindVerificationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<List<VerificationToken>> FindActiveVerificationTokensAsync(Guid memberId, VerificationTokenPurpose purpose, CancellationToken cancellationToken);

    Task<VerificationToken?> FindLatestVerificationTokenAsync(Guid memberId, VerificationTokenPurpose purpose, CancellationToken cancellationToken);

    void AddMember(Member member);

    void AddVerificationToken(VerificationToken verificationToken);

    void AddOutboxMessage(OutboxMessage outboxMessage);

    /// <summary>
    /// 以單一原子 UPDATE ... RETURNING 遞增登入失敗次數，達門檻時同時設定鎖定截止時間。
    /// 避免多個並行請求各自讀取-遞增-寫回（Lost Update），確保並行密碼錯誤時計數精確累加。
    /// </summary>
    Task<(int FailedLoginAttempts, DateTimeOffset? LockoutEndAt)?> RegisterFailedLoginAsync(
        Guid memberId,
        DateTimeOffset now,
        int maxAttempts,
        TimeSpan lockoutDuration,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
