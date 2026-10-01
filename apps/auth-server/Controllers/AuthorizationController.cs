using System.Security.Claims;
using AuthServer.Infrastructure;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Controllers;

public class AuthorizationController(
    IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations,
    ConsentTicketService tickets,
    TimeProvider timeProvider,
    IConfiguration configuration) : Controller
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

            var returnUrl = PublicUrl.Absolute(this.Request, configuration, this.Request.Path + this.Request.QueryString.Value);
            return this.Redirect(QueryHelpers.AddQueryString(loginUrl, "returnUrl", returnUrl));
        }

        var application = await applications.FindByClientIdAsync(request.ClientId!)
                          ?? throw new InvalidOperationException("Client 應已由 OpenIddict 驗證存在。");

        var memberId = session.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var applicationId = (await applications.GetIdAsync(application))!;
        string authorizationId;

        if (await applications.GetConsentTypeAsync(application) == ConsentTypes.Implicit)
        {
            // 第一方免同意也要建立授權紀錄，Refresh Token 才能掛在其下，重複使用時才撤銷得了整個授權。
            // ponytail: Ad-hoc 授權紀錄不會自動清除，量大時加入 OpenIddict.Quartz 的 Prune 排程。
            authorizationId = await authorizations.CreateForMemberAsync(
                applicationId, memberId, AuthorizationTypes.AdHoc, request.GetScopes(), timeProvider.GetUtcNow());
        }
        else
        {
            // 第三方 Client：找得到會員先前同意過（涵蓋本次範疇）的授權紀錄才免詢問，否則進入 Consent 流程。
            var existing = await authorizations.FindAsync(
                memberId, applicationId, Statuses.Valid, AuthorizationTypes.Permanent, [.. request.GetScopes()])
                .FirstOrDefaultAsync();
            if (existing is null)
            {
                // OIDC：prompt=none 不得出現同意畫面，須以 consent_required 回報。
                if (request.HasPromptValue(PromptValues.None))
                {
                    return this.Forbid(
                        new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "此應用程式需要會員授權同意。",
                        }),
                        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                }

                var consentId = tickets.Issue(
                    Guid.Parse(memberId), request.ClientId!, request.RedirectUri!, request.State, [.. request.GetScopes()],
                    this.Request.QueryString.Value ?? string.Empty);
                var consentUrl = configuration["Auth:MemberConsentUrl"] ?? "https://member.1111.com.tw/oauth/consent";
                return this.Redirect(QueryHelpers.AddQueryString(consentUrl, "consent_id", consentId));
            }

            authorizationId = (await authorizations.GetIdAsync(existing))!;
        }

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, memberId);
        identity.SetClaim(MemberSnapshot.SecurityStampClaim, session.Principal.FindFirstValue(MemberSnapshot.SecurityStampClaim));

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetAuthorizationId(authorizationId);
        // Security Stamp 不放任何 JWT，只留在 Authorization Code / Refresh Token 內，換票時用來比對會員密碼是否已變更。
        principal.SetDestinations(claim => claim.Type == MemberSnapshot.SecurityStampClaim
            ? []
            : [Destinations.AccessToken, Destinations.IdentityToken]);

        return this.SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
