using AuthServer.Infrastructure;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Controllers;

public class TokenController(IMemberDirectory members) : Controller
{
    // Code / Refresh Token 的驗證（PKCE、Client 認證、單次使用、輪替與重複使用偵測）皆由 OpenIddict 在到達此處前完成，
    // 這裡只需把已驗證票據內的身分原樣簽發成新的 Token。
    [HttpPost("~/connect/token")]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = this.HttpContext.GetOpenIddictServerRequest()
                      ?? throw new InvalidOperationException("無法取得 OpenID Connect 請求。");

        if (request.IsClientCredentialsGrantType())
        {
            return this.ExchangeClientCredentials(request);
        }

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            throw new InvalidOperationException("不支援的 grant_type。");
        }

        var result = await this.HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal!;

        // 會員改密碼/重設後 Security Stamp 已變更（ADR 0002），先前核發的 Code 與 Refresh Token 一律失效。
        // 一次查詢同時取得 Security Stamp 與 Role；會員不存在（member 為 null）同樣視為登入狀態已變更。
        var member = Guid.TryParse(principal.GetClaim(Claims.Subject), out var memberId)
            ? await members.GetAsync(memberId, this.HttpContext.RequestAborted)
            : null;
        if (member is null || principal.GetClaim(MemberStamp.ClaimType) != member.SecurityStamp)
        {
            return this.Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "會員的登入狀態已變更，請重新授權。",
                }),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // 管理後台的 Token 附上會員「當下」的平台角色（不沿用授權當時的值，降級的管理員換票時立即失去 admin）。
        // 未授予 admin_api 範疇的 Token 一律不帶角色，第三方看不到會員是否為管理員。
        if (principal.HasScope(AuthScopes.AdminApi))
        {
            principal.SetClaim(MemberRole.ClaimType, member.Role);
            principal.Claims.First(claim => claim.Type == MemberRole.ClaimType).SetDestinations(Destinations.AccessToken);
        }

        return this.SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // Server-to-Server：沒有會員參與，Token 的身分是 Client 本身（sub = client_id）。
    // Client 認證與範疇權限已由 OpenIddict 驗證；不核發 Refresh Token 與 ID Token。
    private IActionResult ExchangeClientCredentials(OpenIddictRequest request)
    {
        // openid、offline_access 是會員身分與長期授權相關的範疇，對沒有會員的 M2M 沒有意義。
        if (request.GetScopes().Any(scope => scope is Scopes.OpenId or Scopes.OfflineAccess))
        {
            return this.Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidScope,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Client Credentials 不可要求 openid 或 offline_access 範疇。",
                }),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, request.ClientId);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetDestinations(_ => [Destinations.AccessToken]);

        return this.SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
