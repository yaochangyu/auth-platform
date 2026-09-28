using MemberApi.Entities;

namespace MemberApi.Repositories;

public interface IMemberRepository
{
    Task<Member?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<VerificationToken?> FindVerificationTokenAsync(string token, CancellationToken cancellationToken);

    void AddMember(Member member);

    void AddVerificationToken(VerificationToken verificationToken);

    void AddOutboxMessage(OutboxMessage outboxMessage);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
