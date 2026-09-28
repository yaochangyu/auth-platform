using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemberApi.Repositories;

public class MemberRepository(MemberApiDbContext dbContext) : IMemberRepository
{
    public Task<Member?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return dbContext.Members.SingleOrDefaultAsync(member => member.Email == email, cancellationToken);
    }

    public Task<VerificationToken?> FindVerificationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return dbContext.VerificationTokens
            .Include(verificationToken => verificationToken.Member)
            .SingleOrDefaultAsync(verificationToken => verificationToken.TokenHash == tokenHash, cancellationToken);
    }

    public Task<List<VerificationToken>> FindActiveVerificationTokensAsync(Guid memberId, CancellationToken cancellationToken)
    {
        return dbContext.VerificationTokens
            .Where(token => token.MemberId == memberId && token.UsedAt == null)
            .ToListAsync(cancellationToken);
    }

    public void AddMember(Member member)
    {
        dbContext.Members.Add(member);
    }

    public void AddVerificationToken(VerificationToken verificationToken)
    {
        dbContext.VerificationTokens.Add(verificationToken);
    }

    public void AddOutboxMessage(OutboxMessage outboxMessage)
    {
        dbContext.OutboxMessages.Add(outboxMessage);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
