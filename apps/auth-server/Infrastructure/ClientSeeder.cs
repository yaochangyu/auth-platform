using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Infrastructure;

public static class ClientSeeder
{
    public static async Task SeedAsync(IOpenIddictApplicationManager applications)
    {
        await SeedClientAsync(applications, "member-web-spa", "會員中心", ConsentTypes.Implicit,
            "https://member.1111.com.tw/oauth/callback", "http://localhost:5173/oauth/callback");
        await SeedClientAsync(applications, "demo-third-party-app", "示範第三方應用程式", ConsentTypes.Explicit,
            "https://demo.1111.com.tw/callback");
    }

    private static async Task SeedClientAsync(
        IOpenIddictApplicationManager applications, string clientId, string displayName, string consentType, params string[] redirectUris)
    {
        if (await applications.FindByClientIdAsync(clientId) is not null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = displayName,
            ClientType = ClientTypes.Public,
            ConsentType = consentType,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Email,
                Permissions.Prefixes.Scope + Scopes.OpenId,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        foreach (var uri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        try
        {
            await applications.CreateAsync(descriptor);
        }
        catch (Exception ex) when (ex is DbUpdateException or OpenIddictExceptions.ValidationException)
        {
            // 多個實例同時首次啟動時，另一個實例已寫入同一 Client，視為已種子完成。
        }
    }
}
