using MemberApi.Entities;

namespace MemberApi.Repositories;

public interface IMemberRepository
{
    Task<Member?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<VerificationToken?> FindVerificationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<List<VerificationToken>> FindActiveVerificationTokensAsync(Guid memberId, CancellationToken cancellationToken);

    void AddMember(Member member);

    void AddVerificationToken(VerificationToken verificationToken);

    void AddOutboxMessage(OutboxMessage outboxMessage);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
