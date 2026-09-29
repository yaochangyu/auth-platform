using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Infrastructure;

public static class ClientSeeder
{
    // demoConfidentialSecret 只在設定時才建立示範用（第一方、免同意）Confidential Client，僅限開發/測試環境設定，
    // 避免把固定密鑰寫進程式碼；正式環境不得設定。
    public static async Task SeedAsync(IOpenIddictApplicationManager applications, string? demoConfidentialSecret)
    {
        await SeedClientAsync(applications, "member-web-spa", "會員中心", ConsentTypes.Implicit, clientSecret: null,
            "https://member.1111.com.tw/oauth/callback", "http://localhost:5173/oauth/callback");
        await SeedClientAsync(applications, "demo-third-party-app", "示範第三方應用程式", ConsentTypes.Explicit, clientSecret: null,
            "https://demo.1111.com.tw/callback");

        if (!string.IsNullOrEmpty(demoConfidentialSecret))
        {
            await SeedClientAsync(applications, "demo-confidential-app", "示範後端應用程式", ConsentTypes.Implicit, clientSecret: demoConfidentialSecret,
                "https://demo-backend.1111.com.tw/callback");
        }
    }

    private static async Task SeedClientAsync(
        IOpenIddictApplicationManager applications, string clientId, string displayName, string consentType, string? clientSecret, params string[] redirectUris)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = displayName,
            ClientType = clientSecret is null ? ClientTypes.Public : ClientTypes.Confidential,
            ClientSecret = clientSecret,
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
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
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
            // 種子 Client 以程式碼為準，每次啟動同步（例如新增的 Scope 權限）。
            if (await applications.FindByClientIdAsync(clientId) is { } existing)
            {
                await applications.UpdateAsync(existing, descriptor);
            }
            else
            {
                await applications.CreateAsync(descriptor);
            }
        }
        catch (Exception ex) when (ex is DbUpdateException or OpenIddictExceptions.ValidationException or OpenIddictExceptions.ConcurrencyException)
        {
            // 多個實例同時啟動時，另一個實例已寫入或更新同一 Client，視為已種子完成。
        }
    }
}
