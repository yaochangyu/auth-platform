using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthServer.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Reqnroll;

namespace AuthServer.Tests.Steps;

[Binding]
public class AuthorizeSteps(AuthServerTestBase testBase)
{
    public const string MemberEmail = "member@example.com";
    public const string MemberDisplayName = "測試會員";

    // 測試會員：建立於 2026-01-01，Email 驗證於 2026-01-02 03:04:05；updated_at 取兩者較晚者。
    public static readonly DateTimeOffset MemberCreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset MemberUpdatedAt = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    // 測試中動態建立的 Client（例如 Secret 輪替）統一使用這組回呼網址。
    public static string RedirectUriOf(string clientId) =>
        RedirectUris.GetValueOrDefault(clientId, "https://demo-backend.1111.com.tw/callback");

    public const string Verifier = "test-code-verifier-with-enough-entropy-1234567890";

    private const string State = "state-12345";
    public static readonly Dictionary<string, string> RedirectUris = new()
    {
        ["member-web-spa"] = "https://member.1111.com.tw/oauth/callback",
        ["demo-third-party-app"] = "https://demo.1111.com.tw/callback",
        ["demo-confidential-app"] = "https://demo-backend.1111.com.tw/callback",
        ["developer-web"] = "https://developer.1111.com.tw/oauth/callback",
    };

    private readonly Guid _memberId = Guid.NewGuid();
    private string? _cookie;
    private string _requestUrl = string.Empty;
    private Uri? _location;

    public Guid MemberId => this._memberId;

    public string? Cookie => this._cookie;

    public Uri? Location => this._location;
    private string _redirectUri = string.Empty;
    private string _body = string.Empty;

    [Given("會員已登入且持有有效的主網域 Session Cookie")]
    public async Task Given會員已登入()
    {
        this._cookie = await this.CreateSessionCookieAsync(this._memberId);
    }

    public async Task<string> CreateSessionCookieAsync(Guid memberId)
    {
        await this.SetSecurityStampAsync(memberId, insert: true, "stamp-1");

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, memberId.ToString()),
                new Claim(ClaimTypes.Email, MemberEmail),
                new Claim(ClaimTypes.Name, MemberDisplayName),
                new Claim("security_stamp", "stamp-1"),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        // 以 Auth Server 自己的 Cookie 設定加密，等同 member-api 以共用 Data Protection 金鑰簽發的 Session Cookie。
        var options = testBase.Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), CookieAuthenticationDefaults.AuthenticationScheme);
        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    [Given("該會員的 Security Stamp 已被更新")]
    public Task Given該會員的SecurityStamp已被更新() => this.SetSecurityStampAsync(this._memberId, insert: false, "stamp-2");

    [When("以 Client \"(.*)\" 及合法的 PKCE 參數發起授權請求")]
    [When("再次以 Client \"(.*)\" 及合法的 PKCE 參數發起授權請求")]
    public Task When合法Pkce(string clientId) => this.AuthorizeAsync(clientId, Challenge(), "S256", RedirectUris[clientId]);

    [When("以 Client \"(.*)\" 及合法的 PKCE 參數並帶 prompt=none 發起授權請求")]
    public Task When帶PromptNone(string clientId) => this.AuthorizeAsync(clientId, Challenge(), "S256", RedirectUris[clientId], "none");

    [When("以 Client \"(.*)\" 及合法的 PKCE 參數並要求範疇 \"(.*)\" 發起授權請求")]
    public Task When要求指定範疇(string clientId, string scopes) =>
        this.AuthorizeAsync(clientId, Challenge(), "S256", RedirectUris[clientId], scope: scopes.Replace('、', ' '));

    [When("以 Client \"(.*)\" 發起授權請求且 未提供 code_challenge")]
    public Task When未提供Challenge(string clientId) => this.AuthorizeAsync(clientId, null, null, RedirectUris[clientId]);

    [When("以 Client \"(.*)\" 發起授權請求且 code_challenge_method 為 plain")]
    public Task WhenPlain(string clientId) => this.AuthorizeAsync(clientId, Challenge(), "plain", RedirectUris[clientId]);

    [When("以 Client \"(.*)\" 發起授權請求且 未提供 code_challenge_method")]
    public Task When未提供Method(string clientId) => this.AuthorizeAsync(clientId, Challenge(), null, RedirectUris[clientId]);

    [When("以 Client \"(.*)\" 發起授權請求且 redirect_uri 為未登記的網址")]
    public Task When未登記RedirectUri(string clientId) =>
        this.AuthorizeAsync(clientId, Challenge(), "S256", "https://evil.example.com/callback");

    [Then("重定向目標應為該 Client 已登記的 redirect_uri")]
    public void Then重定向至RedirectUri() =>
        Assert.Equal(this._redirectUri, this._location!.GetLeftPart(UriPartial.Path));

    [Then("重定向網址應帶有 Authorization Code 與原始 state")]
    public void Then帶有CodeAndState()
    {
        var query = QueryHelpers.ParseQuery(this._location!.Query);
        Assert.False(string.IsNullOrEmpty(query["code"]));
        Assert.Equal(State, query["state"]);
    }

    [Then("重定向網址不應帶有 Authorization Code")]
    public void Then不應帶有Code() => Assert.False(QueryHelpers.ParseQuery(this._location!.Query).ContainsKey("code"));

    [Then("重定向網址的 error 參數應為 \"(.*)\"")]
    public void ThenError參數(string expected) =>
        Assert.Equal(expected, QueryHelpers.ParseQuery(this._location!.Query)["error"]);

    [Then("重定向目標應為會員登入頁 \"(.*)\"")]
    public void Then重定向至登入頁(string loginUrl) => Assert.Equal(loginUrl, this._location!.GetLeftPart(UriPartial.Path));

    [Then("returnUrl 應為原始的授權請求網址")]
    public void ThenReturnUrl() =>
        Assert.Equal(this._requestUrl, QueryHelpers.ParseQuery(this._location!.Query)["returnUrl"]);

    [Then("回應內容應包含錯誤碼 \"(.*)\"")]
    public void Then回應內容應包含錯誤碼(string error) => Assert.Contains(error, this._body);

    [Then("回應不應包含 Location 標頭")]
    public void Then無Location() => Assert.Null(testBase.LastResponse!.Headers.Location);

    public async Task AuthorizeAsync(string clientId, string? challenge, string? method, string redirectUri, string? prompt = null, string scope = "openid profile email")
    {
        this._redirectUri = redirectUri;
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = scope,
            ["state"] = State,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = method,
            ["prompt"] = prompt,
        };
        var url = QueryHelpers.AddQueryString("/connect/authorize", query.Where(pair => pair.Value is not null));

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (this._cookie is not null)
        {
            request.Headers.Add("Cookie", this._cookie);
        }

        testBase.LastResponse = await testBase.Client.SendAsync(request);
        this._requestUrl = new Uri(testBase.Client.BaseAddress!, url).AbsoluteUri;
        this._location = testBase.LastResponse.Headers.Location;
        this._body = await testBase.LastResponse.Content.ReadAsStringAsync();
    }

    public static string Challenge() =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

    private async Task SetSecurityStampAsync(Guid memberId, bool insert, string stamp)
    {
        await using var connection = new NpgsqlConnection(TestRunHooks.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = insert
            ? "insert into members (id, security_stamp, email, display_name, email_verified_at, created_at) values (@id, @stamp, @email, @name, @verifiedAt, @createdAt)"
            : "update members set security_stamp = @stamp where id = @id";
        command.Parameters.AddWithValue("id", memberId);
        command.Parameters.AddWithValue("stamp", stamp);
        if (insert)
        {
            command.Parameters.AddWithValue("email", MemberEmail);
            command.Parameters.AddWithValue("name", MemberDisplayName);
            command.Parameters.AddWithValue("verifiedAt", MemberUpdatedAt);
            command.Parameters.AddWithValue("createdAt", MemberCreatedAt);
        }
        await command.ExecuteNonQueryAsync();
    }
}
