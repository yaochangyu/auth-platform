using System.Net.Http.Json;
using System.Text.Json;
using AuthServer.Tests.Support;
using Microsoft.AspNetCore.WebUtilities;
using Npgsql;
using Reqnroll;

namespace AuthServer.Tests.Steps;

[Binding]
public class ConsentSteps(AuthServerTestBase testBase, AuthorizeSteps authorize)
{
    private const string ClientId = "demo-third-party-app";

    private string _consentId = string.Empty;
    private JsonElement _details;
    private Uri? _location;

    [Given("會員已登入且已被導向第三方 Client 的同意頁")]
    public async Task Given已被導向同意頁()
    {
        await authorize.Given會員已登入();
        await authorize.AuthorizeAsync(ClientId, AuthorizeSteps.Challenge(), "S256", AuthorizeSteps.RedirectUris[ClientId]);
        this._consentId = QueryHelpers.ParseQuery(authorize.Location!.Query)["consent_id"]!;
    }

    [Given("時間已經過了 (\\d+) 分鐘")]
    public void Given時間已經過了(int minutes) => testBase.Factory.TimeProvider.Advance(TimeSpan.FromMinutes(minutes));

    [When("前端以該 consent_id 取得同意資訊")]
    public Task When取得同意資訊() => this.GetDetailsAsync(authorize.Cookie);

    [When("未帶 Session Cookie 以該 consent_id 取得同意資訊")]
    public Task When未帶Cookie取得同意資訊() => this.GetDetailsAsync(null);

    [Given("會員勾選範疇 \"(.*)\" 並按下同意")]
    [When("會員勾選範疇 \"(.*)\" 並按下同意")]
    public Task When同意(string scopes) => this.SubmitAsync(this._consentId, authorize.Cookie, "approve", scopes.Split('、'));

    [When("會員按下拒絕")]
    public Task When拒絕() => this.SubmitAsync(this._consentId, authorize.Cookie, "deny");

    [When("會員以偽造的 consent_id 提交同意")]
    public Task When偽造() => this.SubmitAsync("forged-consent-id", authorize.Cookie, "approve", "openid");

    [When("另一位會員以該 consent_id 提交同意")]
    public async Task When另一位會員提交()
    {
        var otherCookie = await authorize.CreateSessionCookieAsync(Guid.NewGuid());
        await this.SubmitAsync(this._consentId, otherCookie, "approve", "openid");
    }

    [Then("重定向目標應為同意頁 \"(.*)\"")]
    public void Then重定向至同意頁(string consentUrl) =>
        Assert.Equal(consentUrl, authorize.Location!.GetLeftPart(UriPartial.Path));

    [Then("重定向網址應帶有 consent_id")]
    public void Then帶有ConsentId() =>
        Assert.False(string.IsNullOrEmpty(QueryHelpers.ParseQuery(authorize.Location!.Query)["consent_id"]));

    [Then("同意資訊的應用程式名稱應為 \"(.*)\"")]
    public void Then應用程式名稱(string expected) =>
        Assert.Equal(expected, this._details.GetProperty("applicationName").GetString());

    [Then("同意資訊應列出範疇 \"(.*)\"")]
    public void Then列出範疇(string scopes) =>
        Assert.Equal(
            scopes.Split('、'),
            this._details.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetProperty("name").GetString()));

    [Then("跟隨重定向後應重定向至第三方 Client 的 redirect_uri 並帶有 Authorization Code 與原始 state")]
    public async Task Then跟隨後帶有Code()
    {
        var response = await this.SendAsync(HttpMethod.Get, testBase.LastResponse!.Headers.Location!.ToString(), authorize.Cookie);
        var location = response.Headers.Location!;
        Assert.Equal(AuthorizeSteps.RedirectUris[ClientId], location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.False(string.IsNullOrEmpty(query["code"]));
        Assert.Equal("state-12345", query["state"]);
    }

    [Then("應重定向至第三方 Client 的 redirect_uri 且 error 為 \"(.*)\" 並帶有原始 state")]
    public void Then拒絕後重定向(string error)
    {
        var location = testBase.LastResponse!.Headers.Location!;
        Assert.Equal(AuthorizeSteps.RedirectUris[ClientId], location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal(error, query["error"]);
        Assert.Equal("state-12345", query["state"]);
    }

    [Then("授權紀錄應包含範疇 \"(.*)\"")]
    public async Task Then授權紀錄應包含(string scope) => Assert.Contains($"\"{scope}\"", await this.GetAuthorizationScopesAsync());

    [Then("授權紀錄不應包含範疇 \"(.*)\"")]
    public async Task Then授權紀錄不應包含(string scope) => Assert.DoesNotContain($"\"{scope}\"", await this.GetAuthorizationScopesAsync());

    private async Task GetDetailsAsync(string? cookie)
    {
        testBase.LastResponse = await this.SendAsync(HttpMethod.Get, $"/api/v1/oauth/consent/{this._consentId}", cookie);
        if (testBase.LastResponse.IsSuccessStatusCode)
        {
            this._details = await testBase.LastResponse.Content.ReadFromJsonAsync<JsonElement>();
        }
    }

    private async Task SubmitAsync(string consentId, string? cookie, string decision, params string[] scopes)
    {
        var form = new List<KeyValuePair<string, string>> { new("decision", decision) };
        form.AddRange(scopes.Select(scope => new KeyValuePair<string, string>("scope", scope)));
        testBase.LastResponse = await this.SendAsync(
            HttpMethod.Post, $"/api/v1/oauth/consent/{consentId}", cookie, new FormUrlEncodedContent(form));
        this._location = testBase.LastResponse.Headers.Location;
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? cookie, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return testBase.Client.SendAsync(request);
    }

    private async Task<string> GetAuthorizationScopesAsync()
    {
        await using var connection = new NpgsqlConnection(TestRunHooks.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select scopes from openiddict_authorizations where subject = @subject";
        command.Parameters.AddWithValue("subject", authorize.MemberId.ToString());
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
