using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Tests.Support;
using Microsoft.IdentityModel.Tokens;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class DualSchemeMemberProfileSteps(PostgreSqlTestBase testBase, ScenarioContext scenarioContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private Member? _currentMember;
    private string? _currentToken;
    private MemberProfileResponse? _profileResponse;

    [Given("初始化測試伺服器與授權中心 JWKS 金鑰環境")]
    public Task Given初始化測試伺服器與授權中心Jwks金鑰環境()
    {
        return testBase.StartAsync();
    }

    [Given("資料庫中存在一名已啟用之會員")]
    public async Task Given資料庫中存在一名已啟用之會員()
    {
        var email = $"dual_scheme_{Guid.NewGuid():N}@example.com";
        this._currentMember = await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberStatus.Active);
        scenarioContext.Set(email, "email");
        scenarioContext.Set(this._currentMember, "member");
    }

    [Given("授權中心已為該會員簽發包含 \"([^\"]*)\" 範疇與合法 \"sub\" 之 RS256 Bearer Token")]
    public void Given授權中心已為該會員簽發包含範疇與合法Sub之BearerToken(string scope)
    {
        var member = this._currentMember ?? scenarioContext.Get<Member>("member");
        Assert.NotNull(member);
        this._currentToken = TestJwtIssuer.Create(member.Id, scope);
        scenarioContext.Set(this._currentToken, "bearerToken");
    }

    [Given("授權中心已為該會員簽發僅包含 \"([^\"]*)\" 但無 \"([^\"]*)\" 範疇之 Bearer Token")]
    public void Given授權中心已為該會員簽發僅包含但無範疇之BearerToken(string scope, string ignored)
    {
        var member = this._currentMember ?? scenarioContext.Get<Member>("member");
        Assert.NotNull(member);
        this._currentToken = TestJwtIssuer.Create(member.Id, scope);
        scenarioContext.Set(this._currentToken, "bearerToken");
    }

    [Given("授權中心簽發了一組 sub 為不存在之會員 GUID 的合法 Bearer Token")]
    public void Given授權中心簽發了一組Sub為不存在之會員Guid的合法BearerToken()
    {
        var nonExistentId = Guid.NewGuid();
        this._currentToken = TestJwtIssuer.Create(nonExistentId, "openid profile");
        scenarioContext.Set(this._currentToken, "bearerToken");
    }

    [When("客戶端於 Authorization 標頭攜帶該 Bearer Token 呼叫取得個人檔案 API")]
    [When("客戶端攜帶該 Token 呼叫取得個人檔案 API")]
    public async Task When客戶端攜帶該BearerToken呼叫取得個人檔案Api()
    {
        var token = this._currentToken ?? scenarioContext.Get<string>("bearerToken");
        await this.GetProfileWithBearerAsync(token);
    }

    [When("客戶端於 Authorization 標頭攜帶偽造簽章或已過期之 Bearer Token 呼叫取得個人檔案 API")]
    public async Task When客戶端於Authorization標頭攜帶偽造簽章或已過期之BearerToken呼叫取得個人檔案Api()
    {
        // 使用另一把不受信任的 RSA 鑰匙簽署以模擬無效簽章
        using var untrustedRsa = RSA.Create(2048);
        var untrustedKey = new RsaSecurityKey(untrustedRsa) { KeyId = "untrusted-key" };
        var invalidToken = TestJwtIssuer.Create(Guid.NewGuid(), "openid profile", signingKey: untrustedKey);
        await this.GetProfileWithBearerAsync(invalidToken);
    }

    [When("客戶端未攜帶任何憑據呼叫取得個人檔案 API")]
    public async Task When客戶端未攜帶任何憑據呼叫取得個人檔案Api()
    {
        await this.GetProfileWithBearerAsync(null);
    }

    [Then("回應內容應包含該會員的 Email、暱稱與狀態")]
    public void Then回應內容應包含該會員的Email暱稱與狀態()
    {
        var email = scenarioContext.Get<string>("email");
        Assert.NotNull(this._profileResponse);
        Assert.Equal(email, this._profileResponse!.Email);
        Assert.False(string.IsNullOrWhiteSpace(this._profileResponse.DisplayName));
        Assert.Equal(MemberStatus.Active, this._profileResponse.Status);
    }

    [Then("回應內容應為符合 RFC 7807 的 Problem Details 錯誤，標題包含 \"([^\"]*)\"")]
    public void Then回應內容應為符合Rfc7807的ProblemDetails錯誤標題包含(string expectedTitleSubstring)
    {
        Assert.NotNull(testBase.LastProblemDetails);
        Assert.False(string.IsNullOrWhiteSpace(testBase.LastProblemDetails!.Type));
        Assert.False(string.IsNullOrWhiteSpace(testBase.LastProblemDetails.Title));
        Assert.Contains(expectedTitleSubstring, testBase.LastProblemDetails.Title);
    }

    private async Task GetProfileWithBearerAsync(string? bearerToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/member/profile");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        var response = await testBase.Client.SendAsync(request);
        testBase.LastResponse = response;

        if (response.StatusCode == HttpStatusCode.OK)
        {
            this._profileResponse = await response.Content.ReadFromJsonAsync<MemberProfileResponse>(JsonOptions);
        }
        else
        {
            var raw = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                Xunit.Assert.Fail($"Got 500: {raw}");
            }
            try
            {
                testBase.LastProblemDetails = System.Text.Json.JsonSerializer.Deserialize<ProblemDetailsPayload>(raw, JsonOptions);
            }
            catch
            {
                // ignore
            }
        }
    }
}
