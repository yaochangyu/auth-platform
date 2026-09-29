using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Infrastructure;

public static class AuthorizationManagerExtensions
{
    public static async Task<string> CreateForMemberAsync(
        this IOpenIddictAuthorizationManager authorizations,
        string applicationId,
        string memberId,
        string type,
        IEnumerable<string> scopes,
        DateTimeOffset now)
    {
        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = applicationId,
            Subject = memberId,
            Type = type,
            Status = Statuses.Valid,
            CreationDate = now,
        };
        descriptor.Scopes.UnionWith(scopes);

        return (await authorizations.GetIdAsync(await authorizations.CreateAsync(descriptor)))!;
    }
}
