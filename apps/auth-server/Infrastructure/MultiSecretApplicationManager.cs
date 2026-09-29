using System.Text.Json;
using AuthShared;
using AuthShared.ClientSecrets;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;

namespace AuthServer.Infrastructure;

/// <summary>
/// OpenIddict 預設一個 Client 只有一組 Secret。這裡改為：Client 的 Properties 內若有 Secret 集合
/// （由 developer-api 管理，見 <see cref="ClientSecretSet"/>），就以集合判定（未過期且未作廢的任一組符合即可），
/// 支援雙金鑰平滑輪替；沒有集合的 Client（例如種子 Client）維持 OpenIddict 原本的單一 Secret 驗證。
/// </summary>
public class MultiSecretApplicationManager<TApplication>(
    IOpenIddictApplicationCache<TApplication> cache,
    ILogger<OpenIddictApplicationManager<TApplication>> logger,
    IOptionsMonitor<OpenIddictCoreOptions> options,
    IOpenIddictApplicationStore<TApplication> store,
    TimeProvider timeProvider) : OpenIddictApplicationManager<TApplication>(cache, logger, options, store)
    where TApplication : class
{
    // 被管理員停用（斷路）的 Client：所有權限檢查（端點、授權方式、範疇）一律不通過，
    // 授權、換票、續約與 Client Credentials 因此全部被拒；Client 設定原樣保留，取消停用即可還原。
    public override async ValueTask<bool> HasPermissionAsync(TApplication application, string permission, CancellationToken cancellationToken = default)
    {
        var properties = await this.Store.GetPropertiesAsync(application, cancellationToken);
        if (properties.TryGetValue(ClientProperties.Suspended, out var suspended) && suspended.ValueKind == JsonValueKind.True)
        {
            return false;
        }

        return await base.HasPermissionAsync(application, permission, cancellationToken);
    }

    public override async ValueTask<bool> ValidateClientSecretAsync(
        TApplication application, string secret, CancellationToken cancellationToken = default)
    {
        var properties = await this.Store.GetPropertiesAsync(application, cancellationToken);

        if (!properties.TryGetValue(ClientSecretSet.PropertyName, out var json))
        {
            return await base.ValidateClientSecretAsync(application, secret, cancellationToken);
        }

        try
        {
            return ClientSecretSet.FromJson(json).IsValid(secret, timeProvider.GetUtcNow());
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
