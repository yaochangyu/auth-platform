using AuthServer.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Controllers;

public class UserInfoController(IMemberDirectory members) : Controller
{
    // Access Token 的簽章、過期與 Bearer 標頭解析由 OpenIddict Validation 處理，失敗時回 401 與 WWW-Authenticate。
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Produces("application/json")]
    public async Task<IActionResult> UserInfo()
    {
        // OIDC Core 5.3.1：UserInfo 端點需要 openid 範疇。
        if (!this.User.HasScope(Scopes.OpenId))
        {
            return this.Forbid(
                Properties(Errors.InsufficientScope, "Access Token 未包含 openid 範疇。"),
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        }

        if (!Guid.TryParse(this.User.GetClaim(Claims.Subject), out var memberId)
            || await members.GetAsync(memberId, this.HttpContext.RequestAborted) is not { } member)
        {
            return this.Challenge(
                Properties(Errors.InvalidToken, "Access Token 對應的會員不存在。"),
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        }

        // 最小權限：只回傳 Access Token 內已授權範疇對應的欄位。
        var claims = new Dictionary<string, object> { [Claims.Subject] = memberId.ToString() };

        if (this.User.HasScope(Scopes.Email))
        {
            claims[Claims.Email] = member.Email;
            claims[Claims.EmailVerified] = member.EmailVerified;
        }

        if (this.User.HasScope(Scopes.Profile))
        {
            claims[Claims.Nickname] = member.DisplayName;
            claims[Claims.UpdatedAt] = member.UpdatedAt.ToUnixTimeSeconds();
        }

        this.Response.Headers.CacheControl = "no-store";
        return this.Ok(claims);
    }

    private static AuthenticationProperties Properties(string error, string description) =>
        new(new Dictionary<string, string?>
        {
            [OpenIddictValidationAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictValidationAspNetCoreConstants.Properties.ErrorDescription] = description,
        });
}
