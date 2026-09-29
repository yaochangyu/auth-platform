using AuthServer.Infrastructure;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Controllers;

public class TokenController(AuthServerDbContext dbContext) : Controller
{
    // Code / Refresh Token 的驗證（PKCE、Client 認證、單次使用、輪替與重複使用偵測）皆由 OpenIddict 在到達此處前完成，
    // 這裡只需把已驗證票據內的身分原樣簽發成新的 Token。
    [HttpPost("~/connect/token")]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = this.HttpContext.GetOpenIddictServerRequest()
                      ?? throw new InvalidOperationException("無法取得 OpenID Connect 請求。");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            throw new InvalidOperationException("不支援的 grant_type。");
        }

        var result = await this.HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal!;

        // 會員改密碼/重設後 Security Stamp 已變更（ADR 0002），先前核發的 Code 與 Refresh Token 一律失效。
        if (!Guid.TryParse(principal.GetClaim(Claims.Subject), out var memberId)
            || principal.GetClaim(MemberStamp.ClaimType) != await MemberStamp.GetCurrentAsync(dbContext, memberId, this.HttpContext.RequestAborted))
        {
            return this.Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "會員的登入狀態已變更，請重新授權。",
                }),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return this.SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
