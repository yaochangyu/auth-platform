namespace MemberApi.Contracts;

public record ConnectedAppListResponse(IReadOnlyList<ConnectedAppDto> Items, int TotalCount);
