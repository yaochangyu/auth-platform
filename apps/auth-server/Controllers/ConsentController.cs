using System.Security.Claims;
using AuthServer.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Controllers;

[Route("api/v1/oauth/consent/{consentId}")]
public class ConsentController(
    ConsentTicketService tickets,
    IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations,
    TimeProvider timeProvider,
    IConfiguration configuration) : ControllerBase
{
    private static readonly Dictionary<string, string> ScopeDescriptions = new()
    {
        [Scopes.OpenId] = "確認您的身分",
        [Scopes.Profile] = "讀取您的基本個人資料（暱稱）",
        [Scopes.Email] = "讀取您的電子郵件",
        [Scopes.OfflineAccess] = "在您離線時持續存取（長期授權）",
    };

    public record ScopeItem(string Name, string Description);

    public record ConsentDetails(string ApplicationName, IReadOnlyList<ScopeItem> Scopes);

    public class DecisionForm
    {
        public string Decision { get; set; } = string.Empty;

        public string[] Scope { get; set; } = [];
    }

    [HttpGet]
    public async Task<IActionResult> Get(string consentId)
    {
        var (ticket, failure) = await this.ResolveAsync(consentId);
        if (ticket is null)
        {
            return failure!;
        }

        var application = await applications.FindByClientIdAsync(ticket.ClientId);
        var name = application is null ? ticket.ClientId : await applications.GetDisplayNameAsync(application) ?? ticket.ClientId;
        return this.Ok(new ConsentDetails(
            name, ticket.Scopes.Select(scope => new ScopeItem(scope, ScopeDescriptions.GetValueOrDefault(scope, scope))).ToList()));
    }

    // 以瀏覽器原生表單提交（application/x-www-form-urlencoded），由此端點直接以 302 導回；
    // 不使用 JSON，是因為 fetch 無法跟隨跨網域的 302 至第三方 redirect_uri。
    // CSRF：需同時具備 Session Cookie（member-api 簽發時已設 SameSite=Lax，瀏覽器不會在跨站 POST 附帶）
    // 與只有該會員頁面取得的 consent_id。
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Post(string consentId, [FromForm] DecisionForm form)
    {
        var (ticket, failure) = await this.ResolveAsync(consentId);
        if (ticket is null)
        {
            return failure!;
        }

        if (!tickets.TryConsume(ticket))
        {
            return this.Problem(statusCode: StatusCodes.Status400BadRequest, title: "consent_id 已使用過");
        }

        // 只能在票證請求的範疇內縮減，不能新增；openid 是識別身分的必要範疇，不可被移除。
        var granted = ticket.Scopes.Where(scope => scope == Scopes.OpenId || form.Scope.Contains(scope)).ToArray();
        if (form.Decision != "approve" || granted.Length == 0)
        {
            var denied = QueryHelpers.AddQueryString(ticket.RedirectUri, "error", Errors.AccessDenied);
            return this.Redirect(ticket.State is null ? denied : QueryHelpers.AddQueryString(denied, "state", ticket.State));
        }

        var application = await applications.FindByClientIdAsync(ticket.ClientId)
                          ?? throw new InvalidOperationException("票證中的 Client 應存在。");
        await authorizations.CreateForMemberAsync(
            (await applications.GetIdAsync(application))!,
            ticket.MemberId.ToString(),
            AuthorizationTypes.Permanent,
            granted,
            timeProvider.GetUtcNow());

        // 回到授權端點，由它找到剛建立的授權紀錄並核發 Authorization Code。
        var query = QueryHelpers.ParseQuery(ticket.AuthorizeQuery)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
        query["scope"] = string.Join(' ', granted);
        return this.Redirect(PublicUrl.Absolute(this.Request, configuration, QueryHelpers.AddQueryString("/connect/authorize", query!)));
    }

    private async Task<(ConsentTicket? Ticket, IActionResult? Failure)> ResolveAsync(string consentId)
    {
        var session = await this.HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!session.Succeeded)
        {
            return (null, this.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "未提供有效 Session Cookie 或會話已逾期失效"));
        }

        var ticket = tickets.Read(consentId);
        if (ticket is null)
        {
            return (null, this.Problem(statusCode: StatusCodes.Status400BadRequest, title: "consent_id 無效或已過期"));
        }

        if (session.Principal!.FindFirstValue(ClaimTypes.NameIdentifier) != ticket.MemberId.ToString())
        {
            return (null, this.Problem(statusCode: StatusCodes.Status403Forbidden, title: "此 consent_id 不屬於目前登入的會員"));
        }

        return (ticket, null);
    }
}
