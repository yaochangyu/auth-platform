using MemberApi.Contracts;
using MemberApi.Repositories;

namespace MemberApi.Handlers;

public class ListConnectedAppsHandler(IConnectedAppRepository connectedAppRepository) : IListConnectedAppsHandler
{
    public async Task<ConnectedAppListResponse> HandleAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var grants = await connectedAppRepository.FindActiveGrantsAsync(memberId, cancellationToken);

        var items = grants
            .Select(grant => new ConnectedAppDto(
                grant.Id,
                grant.ConnectedApp!.Name,
                grant.ConnectedApp.Identifier,
                grant.ConnectedApp.LogoUrl,
                grant.Scopes,
                grant.AuthorizedAt,
                grant.LastUsedAt))
            .ToList();

        return new ConnectedAppListResponse(items, items.Count);
    }
}
