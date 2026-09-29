using MemberApi.Repositories;

namespace MemberApi.Handlers;

public class RevokeConnectedAppHandler(IConnectedAppRepository connectedAppRepository, TimeProvider timeProvider) : IRevokeConnectedAppHandler
{
    public async Task<RevokeConnectedAppOutcome> HandleAsync(Guid memberId, Guid grantId, CancellationToken cancellationToken)
    {
        var grant = await connectedAppRepository.FindActiveGrantAsync(grantId, memberId, cancellationToken);
        if (grant is null)
        {
            return RevokeConnectedAppOutcome.NotFound;
        }

        grant.RevokedAt = timeProvider.GetUtcNow();
        await connectedAppRepository.SaveChangesAsync(cancellationToken);

        return RevokeConnectedAppOutcome.Success;
    }
}
