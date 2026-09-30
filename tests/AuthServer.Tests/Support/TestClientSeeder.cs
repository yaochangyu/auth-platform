using System.Text.Json;
using AuthShared;
using AuthShared.ClientSecrets;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Tests.Support;

// 測試用的 Client 種子：以與 developer-api 相同的 ClientSecretSet 寫入 Confidential Client。
// OpenIddict 要求 Confidential Client 必須有 client_secret，這裡放不會被使用的佔位值；實際驗證只看 Secret 集合。
public static class TestClientSeeder
{
    public const string PlaceholderSecret = "placeholder-never-used-for-authentication";

    public static async Task SaveConfidentialAsync(
        IServiceProvider services, string clientId, ClientSecretSet secrets, bool clientCredentials, bool suspended, params string[] scopes)
    {
        await using var scope = services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = "測試 Client",
            ClientType = ClientTypes.Confidential,
            ClientSecret = PlaceholderSecret,
            ConsentType = ConsentTypes.Implicit,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
            RedirectUris = { new Uri("https://demo-backend.1111.com.tw/callback") },
        };
        foreach (var name in scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + name);
        }

        if (clientCredentials)
        {
            descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        }

        descriptor.Properties[ClientSecretSet.PropertyName] = secrets.ToJson();
        if (suspended)
        {
            descriptor.Properties[ClientProperties.Suspended] = JsonSerializer.SerializeToElement(true);
        }

        if (await applications.FindByClientIdAsync(clientId) is { } existing)
        {
            await applications.UpdateAsync(existing, descriptor);
        }
        else
        {
            await applications.CreateAsync(descriptor);
        }
    }

    // 讓已存在的 Client 之後才被管理員停用（斷路），用於驗證停用前取得的授權碼也不能再換票。
    public static async Task SuspendAsync(IServiceProvider services, string clientId)
    {
        await using var scope = services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var client = (await applications.FindByClientIdAsync(clientId))!;
        var descriptor = new OpenIddictApplicationDescriptor();
        await applications.PopulateAsync(descriptor, client);
        descriptor.Properties[ClientProperties.Suspended] = JsonSerializer.SerializeToElement(true);
        await applications.UpdateAsync(client, descriptor);
    }

    public static async Task SavePublicAsync(IServiceProvider services, string clientId)
    {
        await using var scope = services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = "測試 Public Client",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            Permissions = { Permissions.Endpoints.Token, Permissions.GrantTypes.AuthorizationCode },
        };

        if (await applications.FindByClientIdAsync(clientId) is { } existing)
        {
            await applications.UpdateAsync(existing, descriptor);
        }
        else
        {
            await applications.CreateAsync(descriptor);
        }
    }

    public static async Task SavePublicAppAsync(
        IServiceProvider services, string clientId, string redirectUri = "https://app.1111.com.tw/oauth/callback", params string[] scopes)
    {
        await using var scope = services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = "原生 App 公用客戶端",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
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
            RedirectUris = { new Uri(redirectUri) },
        };
        foreach (var name in scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + name);
        }

        if (await applications.FindByClientIdAsync(clientId) is { } existing)
        {
            await applications.PopulateAsync(descriptor, existing);
            await applications.UpdateAsync(existing, descriptor);
        }
        else
        {
            await applications.CreateAsync(descriptor);
        }
    }
}
