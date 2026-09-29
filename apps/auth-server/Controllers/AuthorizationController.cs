using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Controllers;

public class AuthorizationController(IOpenIddictApplicationManager applications, IConfiguration configuration) : Controller
{
    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = this.HttpContext.GetOpenIddictServerRequest()
                      ?? throw new InvalidOperationException("無法取得 OpenID Connect 請求。");

        var session = await this.HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!session.Succeeded)
        {
            // OIDC：prompt=none 不得出現登入畫面，須以 login_required 回報（Silent SSO 靜默續期用）。
            if (request.HasPromptValue(PromptValues.None))
            {
                return this.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "會員尚未登入。",
                    }),
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var loginUrl = configuration["Auth:MemberLoginUrl"] ?? "https://member.1111.com.tw/login";

            // 經 Proxy 時 Host 會是內部位址，已設定對外 issuer 時以它為準組出回跳網址。
            var returnUrl = configuration["Auth:Issuer"] is { Length: > 0 } publicBase
                ? publicBase.TrimEnd('/') + this.Request.PathBase + this.Request.Path + this.Request.QueryString
                : this.Request.GetEncodedUrl();
            return this.Redirect(QueryHelpers.AddQueryString(loginUrl, "returnUrl", returnUrl));
        }

        var application = await applications.FindByClientIdAsync(request.ClientId!)
                          ?? throw new InvalidOperationException("Client 應已由 OpenIddict 驗證存在。");

        // 第三方 Client 的 Consent 流程於 Issue #13 實作，此處先不核發 Authorization Code。
        if (await applications.GetConsentTypeAsync(application) != ConsentTypes.Implicit)
        {
            return this.Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "此應用程式需要會員授權同意。",
                }),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, session.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetDestinations(_ => [Destinations.AccessToken, Destinations.IdentityToken]);

        return this.SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
