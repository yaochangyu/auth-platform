using MemberApi.Infrastructure.Persistence;

namespace MemberApi.Repositories;

public class HealthRepository(MemberApiDbContext dbContext) : IHealthRepository
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        return dbContext.Database.CanConnectAsync(cancellationToken);
    }
}
