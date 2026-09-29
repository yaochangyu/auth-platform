namespace MemberApi.Contracts;

public record ConnectedAppDto(
    Guid AppId,
    string AppName,
    string AppIdentifier,
    string? LogoUrl,
    string[] Scopes,
    DateTimeOffset AuthorizedAt,
    DateTimeOffset? LastUsedAt);
