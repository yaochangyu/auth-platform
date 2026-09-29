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
    private const string State = "state-12345";
    private static readonly Dictionary<string, string> RedirectUris = new()
    {
        ["member-web-spa"] = "https://member.1111.com.tw/oauth/callback",
        ["demo-third-party-app"] = "https://demo.1111.com.tw/callback",
    };

    private readonly Guid _memberId = Guid.NewGuid();
    private string? _cookie;
    private string _requestUrl = string.Empty;
    private Uri? _location;
    private string _redirectUri = string.Empty;
    private string _body = string.Empty;

    [Given("會員已登入且持有有效的主網域 Session Cookie")]
    public async Task Given會員已登入()
    {
        await this.SetSecurityStampAsync(insert: true, "stamp-1");

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, this._memberId.ToString()),
                new Claim(ClaimTypes.Email, "member@example.com"),
                new Claim(ClaimTypes.Name, "測試會員"),
                new Claim("security_stamp", "stamp-1"),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        // 以 Auth Server 自己的 Cookie 設定加密，等同 member-api 以共用 Data Protection 金鑰簽發的 Session Cookie。
        var options = testBase.Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), CookieAuthenticationDefaults.AuthenticationScheme);
        this._cookie = $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    [Given("該會員的 Security Stamp 已被更新")]
    public Task Given該會員的SecurityStamp已被更新() => this.SetSecurityStampAsync(insert: false, "stamp-2");

    [When("以 Client \"(.*)\" 及合法的 PKCE 參數發起授權請求")]
    public Task When合法Pkce(string clientId) => this.AuthorizeAsync(clientId, Challenge(), "S256", RedirectUris[clientId]);

    [When("以 Client \"(.*)\" 及合法的 PKCE 參數並帶 prompt=none 發起授權請求")]
    public Task When帶PromptNone(string clientId) => this.AuthorizeAsync(clientId, Challenge(), "S256", RedirectUris[clientId], "none");

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

    private async Task AuthorizeAsync(string clientId, string? challenge, string? method, string redirectUri, string? prompt = null)
    {
        this._redirectUri = redirectUri;
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid profile email",
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

    private static string Challenge() =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes("test-code-verifier-with-enough-entropy-1234567890")));

    private async Task SetSecurityStampAsync(bool insert, string stamp)
    {
        await using var connection = new NpgsqlConnection(TestRunHooks.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = insert
            ? "insert into members (id, security_stamp) values (@id, @stamp)"
            : "update members set security_stamp = @stamp where id = @id";
        command.Parameters.AddWithValue("id", this._memberId);
        command.Parameters.AddWithValue("stamp", stamp);
        await command.ExecuteNonQueryAsync();
    }
}
