using DeveloperApi.Entities;
using DeveloperApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DeveloperApi.Repositories;

// Application Ownership 的隔離邊界：每個查詢都必須帶擁有者 MemberId，呼叫端無法繞過。
public class ApplicationRepository(DeveloperApiDbContext dbContext)
{
    public Task<List<DeveloperApplication>> ListAsync(Guid ownerMemberId, CancellationToken cancellationToken) =>
        dbContext.Applications
            .Where(application => application.OwnerMemberId == ownerMemberId)
            .OrderByDescending(application => application.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<DeveloperApplication?> FindAsync(Guid ownerMemberId, Guid applicationId, CancellationToken cancellationToken) =>
        dbContext.Applications.SingleOrDefaultAsync(
            application => application.Id == applicationId && application.OwnerMemberId == ownerMemberId, cancellationToken);

    public void Add(DeveloperApplication application) => dbContext.Applications.Add(application);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
