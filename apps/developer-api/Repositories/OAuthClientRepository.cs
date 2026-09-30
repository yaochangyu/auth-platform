using System.Security.Cryptography;
using AuthShared.ClientSecrets;
using DeveloperApi.Contracts;
using DeveloperApi.Entities;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DeveloperApi.Repositories;

public record OAuthClientState(
    OAuthClientType ClientType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes,
    ClientSecretSet Secrets);

public enum IssueSecretOutcome
{
    Issued,
    NotConfidential,
    LimitReached,
}

// OAuth Client 設定的存取層：Client 的權威資料存在 auth-server 使用的 OpenIddict 資料表（以 ClientId 對應專案），
// developer-api 用 OpenIddict 管理器讀寫。
// ponytail: 兩個服務共用資料庫直接寫入（同 auth-server 讀 members 的作法）；有內部管理 API 後改為呼叫 API。
public class OAuthClientRepository(IOpenIddictApplicationManager applications, TimeProvider timeProvider, IConfiguration configuration)
{
    // 開發者可申請的範疇。developer_api 是平台內部 Client 專用，不開放。
    public static readonly IReadOnlyList<string> AllowedScopes = [Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess];

    private TimeSpan GracePeriod => TimeSpan.FromHours(configuration.GetValue("OAuthClient:SecretGraceHours", 24));

    public async Task<OAuthClientState> GetAsync(string clientId, CancellationToken cancellationToken)
    {
        if (await applications.FindByClientIdAsync(clientId, cancellationToken) is not { } application)
        {
            return new OAuthClientState(OAuthClientType.Public, [], [], [], ClientSecretSet.Empty);
        }

        var properties = await applications.GetPropertiesAsync(application, cancellationToken);
        return new OAuthClientState(
            await applications.GetClientTypeAsync(application, cancellationToken) == ClientTypes.Confidential
                ? OAuthClientType.Confidential
                : OAuthClientType.Public,
            [.. await applications.GetRedirectUrisAsync(application, cancellationToken)],
            [.. await applications.GetPostLogoutRedirectUrisAsync(application, cancellationToken)],
            [.. (await applications.GetPermissionsAsync(application, cancellationToken))
                .Where(permission => permission.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
                .Select(permission => permission[Permissions.Prefixes.Scope.Length..])],
            properties.TryGetValue(ClientSecretSet.PropertyName, out var secrets) ? ClientSecretSet.FromJson(secrets) : ClientSecretSet.Empty);
    }

    public async Task SaveConfigAsync(DeveloperApplication project, OAuthClientRequest request, CancellationToken cancellationToken)
    {
        var existing = await applications.FindByClientIdAsync(project.ClientId, cancellationToken);
        var descriptor = new OpenIddictApplicationDescriptor();
        if (existing is not null)
        {
            // 保留既有的 Secret 集合與（已雜湊的）client_secret，只改設定欄位。
            await applications.PopulateAsync(descriptor, existing, cancellationToken);
        }

        descriptor.ClientId = project.ClientId;
        descriptor.DisplayName = project.Name;
        // 開發者建立的 Client 都是第三方，一律需要會員明確同意；免同意只保留給平台自己的第一方 Client。
        descriptor.ConsentType = ConsentTypes.Explicit;
        descriptor.ClientType = request.ClientType == OAuthClientType.Confidential ? ClientTypes.Confidential : ClientTypes.Public;
        SetUris(descriptor.RedirectUris, request.RedirectUris);
        SetUris(descriptor.PostLogoutRedirectUris, request.PostLogoutRedirectUris);

        descriptor.Permissions.Clear();
        descriptor.Permissions.UnionWith([
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.ResponseTypes.Code,
            .. request.Scopes.Select(scope => Permissions.Prefixes.Scope + scope),
        ]);
        if (request.Scopes.Contains(Scopes.OfflineAccess))
        {
            descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
        }

        // Confidential Client 有後端可安全保管 Secret，可直接以 client_id + client_secret 換發 M2M Access Token。
        if (request.ClientType == OAuthClientType.Confidential)
        {
            descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        }

        descriptor.Requirements.Clear();
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

        if (request.ClientType == OAuthClientType.Confidential)
        {
            // OpenIddict 規定 Confidential Client 必須有 client_secret；這裡放一組隨機且不會被使用的佔位值。
            // 實際驗證只看 Properties 內的 Secret 集合（見 auth-server 的 MultiSecretApplicationManager）。
            descriptor.ClientSecret ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            descriptor.Properties.TryAdd(ClientSecretSet.PropertyName, ClientSecretSet.Empty.ToJson());
        }
        else
        {
            // 改為 Public：不再有 Secret，既有的一併清除。
            descriptor.ClientSecret = null;
            descriptor.Properties.Remove(ClientSecretSet.PropertyName);
        }

        if (existing is null)
        {
            await applications.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await applications.UpdateAsync(existing, descriptor, cancellationToken);
        }
    }

    public async Task<(IssueSecretOutcome Outcome, string? Plaintext, ClientSecretEntry? Entry)> IssueSecretAsync(
        string clientId, CancellationToken cancellationToken)
    {
        if (await applications.FindByClientIdAsync(clientId, cancellationToken) is not { } application
            || await applications.GetClientTypeAsync(application, cancellationToken) != ClientTypes.Confidential)
        {
            return (IssueSecretOutcome.NotConfidential, null, null);
        }

        var secrets = await this.ReadSecretsAsync(application, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (secrets.ActiveCount(now) >= ClientSecretSet.MaxActive)
        {
            return (IssueSecretOutcome.LimitReached, null, null);
        }

        var (updated, plaintext, entry) = secrets.Issue(now, this.GracePeriod);
        await this.WriteSecretsAsync(application, updated, cancellationToken);
        return (IssueSecretOutcome.Issued, plaintext, entry);
    }

    public async Task<bool> RevokeSecretAsync(string clientId, Guid secretId, CancellationToken cancellationToken)
    {
        if (await applications.FindByClientIdAsync(clientId, cancellationToken) is not { } application)
        {
            return false;
        }

        var revoked = (await this.ReadSecretsAsync(application, cancellationToken)).Revoke(secretId, timeProvider.GetUtcNow());
        if (revoked is null)
        {
            return false;
        }

        await this.WriteSecretsAsync(application, revoked, cancellationToken);
        return true;
    }

    // 專案改名時同步顯示名稱（會員在同意畫面看到的是這個名稱）。尚未設定 OAuth Client 時不需處理。
    public async Task RenameAsync(string clientId, string displayName, CancellationToken cancellationToken)
    {
        if (await applications.FindByClientIdAsync(clientId, cancellationToken) is not { } application)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applications.PopulateAsync(descriptor, application, cancellationToken);
        descriptor.DisplayName = displayName;
        await applications.UpdateAsync(application, descriptor, cancellationToken);
    }

    private static void SetUris(ISet<Uri> target, IEnumerable<string> uris)
    {
        target.Clear();
        target.UnionWith(uris.Select(uri => new Uri(uri)));
    }

    private async Task<ClientSecretSet> ReadSecretsAsync(object application, CancellationToken cancellationToken) =>
        (await applications.GetPropertiesAsync(application, cancellationToken)).TryGetValue(ClientSecretSet.PropertyName, out var json)
            ? ClientSecretSet.FromJson(json)
            : ClientSecretSet.Empty;

    private async Task WriteSecretsAsync(object application, ClientSecretSet secrets, CancellationToken cancellationToken)
    {
        var descriptor = new OpenIddictApplicationDescriptor();
        await applications.PopulateAsync(descriptor, application, cancellationToken);
        descriptor.Properties[ClientSecretSet.PropertyName] = secrets.ToJson();
        await applications.UpdateAsync(application, descriptor, cancellationToken);
    }
}
