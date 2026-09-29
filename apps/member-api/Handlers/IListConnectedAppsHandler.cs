using MemberApi.Contracts;

namespace MemberApi.Handlers;

public interface IListConnectedAppsHandler
{
    Task<ConnectedAppListResponse> HandleAsync(Guid memberId, CancellationToken cancellationToken);
}
