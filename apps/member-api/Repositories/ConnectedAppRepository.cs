using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemberApi.Repositories;

public class ConnectedAppRepository(MemberApiDbContext dbContext) : IConnectedAppRepository
{
    public Task<List<MemberGrant>> FindActiveGrantsAsync(Guid memberId, CancellationToken cancellationToken)
    {
        return dbContext.MemberGrants
            .Include(grant => grant.ConnectedApp)
            .Where(grant => grant.MemberId == memberId && grant.RevokedAt == null)
            .ToListAsync(cancellationToken);
    }

    public Task<MemberGrant?> FindActiveGrantAsync(Guid grantId, Guid memberId, CancellationToken cancellationToken)
    {
        return dbContext.MemberGrants
            .SingleOrDefaultAsync(
                grant => grant.Id == grantId && grant.MemberId == memberId && grant.RevokedAt == null,
                cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
