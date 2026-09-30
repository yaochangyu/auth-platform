using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Infrastructure;

public static class ClientSeeder
{
    // demoConfidentialSecret 只在設定時才建立示範用（第一方、免同意）Confidential Client，僅限開發/測試環境設定，
    // 避免把固定密鑰寫進程式碼；正式環境不得設定。
    public static async Task SeedAsync(IOpenIddictApplicationManager applications, string? demoConfidentialSecret, IConfiguration configuration)
    {
        string[] standardScopes = [Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess];

        // 內建的回呼網址是正式網域與 localhost；容器編排等環境用其他網域或埠號時，以 Auth:SeedRedirectUris:<clientId> 追加。
        // 只在 Auth:RequireHttps 為 false（本機/容器環境）時生效：正式環境即使有人能改環境變數，
        // 也不能替免同意的第一方 Client 登記任意回呼網址。
        var allowExtra = !configuration.GetValue("Auth:RequireHttps", true);
        string[] Extra(string clientId) => allowExtra ? configuration.GetSection($"Auth:SeedRedirectUris:{clientId}").Get<string[]>() ?? [] : [];

        await SeedClientAsync(applications, "member-web-spa", "會員中心", ConsentTypes.Implicit, clientSecret: null, standardScopes,
            ["https://member.1111.com.tw/oauth/callback", "http://localhost:5173/oauth/callback", .. Extra("member-web-spa")]);
        await SeedClientAsync(applications, "native-mobile-app", "官方行動 App", ConsentTypes.Implicit, clientSecret: null, standardScopes,
            ["https://app.1111.com.tw/oauth/callback", .. Extra("native-mobile-app")]);
        await SeedClientAsync(applications, "demo-third-party-app", "示範第三方應用程式", ConsentTypes.Explicit, clientSecret: null, standardScopes,
            ["https://demo.1111.com.tw/callback", .. Extra("demo-third-party-app")]);

        // 開發者後台為第一方 SPA（Dogfooding）：未授予 offline_access 範疇，因此不會核發 Refresh Token；
        // Access Token 過期時以 SSO 無感重新授權。
        await SeedClientAsync(applications, "developer-web", "開發者後台", ConsentTypes.Implicit, clientSecret: null,
            [Scopes.OpenId, Scopes.Profile, Scopes.Email, AuthScopes.DeveloperApi],
            ["https://developer.1111.com.tw/oauth/callback", "http://localhost:5174/oauth/callback", .. Extra("developer-web")]);

        // 管理後台同樣是第一方 SPA：授予 admin_api 範疇，角色由換票當下的會員資料決定，非管理員拿到的 Token 會被 admin-api 拒絕。
        await SeedClientAsync(applications, "admin-web", "管理後台", ConsentTypes.Implicit, clientSecret: null,
            [Scopes.OpenId, Scopes.Profile, AuthScopes.AdminApi],
            ["https://admin.1111.com.tw/oauth/callback", "http://localhost:5175/oauth/callback", .. Extra("admin-web")]);

        if (!string.IsNullOrEmpty(demoConfidentialSecret))
        {
            await SeedClientAsync(applications, "demo-confidential-app", "示範後端應用程式", ConsentTypes.Implicit, clientSecret: demoConfidentialSecret,
                standardScopes, ["https://demo-backend.1111.com.tw/callback", .. Extra("demo-confidential-app")]);
        }
    }

    private static async Task SeedClientAsync(
        IOpenIddictApplicationManager applications, string clientId, string displayName, string consentType, string? clientSecret,
        string[] scopes, string[] redirectUris)
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
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        foreach (var scope in scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

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
