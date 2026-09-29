namespace MemberApi.Handlers;

public interface IRevokeConnectedAppHandler
{
    Task<RevokeConnectedAppOutcome> HandleAsync(Guid memberId, Guid grantId, CancellationToken cancellationToken);
}
