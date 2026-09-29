using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Tests.Support;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class MemberProfileSteps(PostgreSqlTestBase testBase, ScenarioContext scenarioContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private HttpResponseMessage _response = null!;
    private MemberProfileResponse? _profileResponse;
    private string? _reissuedSessionCookie;

    [When("使用者攜帶該 Session Cookie 呼叫取得個人檔案 API")]
    public async Task When使用者攜帶該SessionCookie呼叫取得個人檔案Api()
    {
        var cookie = scenarioContext.Get<string>("sessionCookie");
        await this.GetProfileAsync(cookie);
    }

    [When("使用者未攜帶 Session Cookie 呼叫取得個人檔案 API")]
    public async Task When使用者未攜帶SessionCookie呼叫取得個人檔案Api()
    {
        await this.GetProfileAsync(null);
    }

    [Then("回應內容應包含會員的 Email、暱稱與狀態")]
    public void Then回應內容應包含會員的EmailDisplayNameStatus()
    {
        var email = scenarioContext.Get<string>("email");
        Assert.NotNull(this._profileResponse);
        Assert.Equal(email, this._profileResponse!.Email);
        Assert.False(string.IsNullOrWhiteSpace(this._profileResponse.DisplayName));
        Assert.Equal(MemberStatus.Active, this._profileResponse.Status);
    }

    [When("使用者攜帶該 Session Cookie 以舊密碼 \"([^\"]*)\" 設定新密碼 \"([^\"]*)\" 並確認密碼 \"([^\"]*)\" 呼叫變更密碼 API")]
    public async Task When使用者攜帶該SessionCookie以舊密碼設定新密碼並確認密碼呼叫變更密碼Api(string currentPassword, string newPassword, string confirmPassword)
    {
        var cookie = scenarioContext.Get<string>("sessionCookie");
        await this.PutChangePasswordAsync(cookie, currentPassword, newPassword, confirmPassword);
    }

    [When("使用者未攜帶 Session Cookie 以舊密碼 \"([^\"]*)\" 設定新密碼 \"([^\"]*)\" 並確認密碼 \"([^\"]*)\" 呼叫變更密碼 API")]
    public async Task When使用者未攜帶SessionCookie以舊密碼設定新密碼並確認密碼呼叫變更密碼Api(string currentPassword, string newPassword, string confirmPassword)
    {
        await this.PutChangePasswordAsync(null, currentPassword, newPassword, confirmPassword);
    }

    [Then("回應標頭應包含重新發行的 Session Cookie")]
    public void Then回應標頭應包含重新發行的SessionCookie()
    {
        Assert.NotNull(this._reissuedSessionCookie);
    }

    [Given("使用者已於「裝置 A」成功登入並取得有效的 Session Cookie")]
    public async Task Given使用者已於裝置A成功登入並取得有效的SessionCookie()
    {
        const string email = "member-profile-multidevice@1111.com.tw";
        const string password = "P@ssw0rd2026!";
        await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberStatus.Active, password);
        var cookie = await this.LoginAndGetCookieAsync(email, password);

        scenarioContext.Set(email, "email");
        scenarioContext.Set(password, "password");
        scenarioContext.Set(cookie, "sessionCookieA");
    }

    [Given("使用者已於「裝置 B」以相同帳號成功登入並取得另一組有效的 Session Cookie")]
    public async Task Given使用者已於裝置B以相同帳號成功登入並取得另一組有效的SessionCookie()
    {
        var email = scenarioContext.Get<string>("email");
        var password = scenarioContext.Get<string>("password");
        var cookie = await this.LoginAndGetCookieAsync(email, password);
        scenarioContext.Set(cookie, "sessionCookieB");
    }

    [When("使用者攜帶「裝置 A」的 Session Cookie 以舊密碼 \"([^\"]*)\" 設定新密碼 \"([^\"]*)\" 並確認密碼 \"([^\"]*)\" 呼叫變更密碼 API")]
    public async Task When使用者攜帶裝置A的SessionCookie以舊密碼設定新密碼並確認密碼呼叫變更密碼Api(string currentPassword, string newPassword, string confirmPassword)
    {
        var cookie = scenarioContext.Get<string>("sessionCookieA");
        await this.PutChangePasswordAsync(cookie, currentPassword, newPassword, confirmPassword);
    }

    [Then("使用者攜帶回應中重新發行的 Session Cookie 呼叫取得個人檔案 API 應成功")]
    public async Task Then使用者攜帶回應中重新發行的SessionCookie呼叫取得個人檔案Api應成功()
    {
        Assert.NotNull(this._reissuedSessionCookie);
        await this.GetProfileAsync(this._reissuedSessionCookie);
        Assert.Equal(HttpStatusCode.OK, this._response.StatusCode);
    }

    [Then("使用者攜帶「裝置 B」的 Session Cookie 呼叫取得個人檔案 API 應回應 401")]
    public async Task Then使用者攜帶裝置B的SessionCookie呼叫取得個人檔案Api應回應401()
    {
        var cookie = scenarioContext.Get<string>("sessionCookieB");
        await this.GetProfileAsync(cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, this._response.StatusCode);
    }

    private async Task GetProfileAsync(string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/member/profile");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        this._response = await testBase.Client.SendAsync(request);
        testBase.LastResponse = this._response;

        if (this._response.StatusCode == HttpStatusCode.OK)
        {
            this._profileResponse = await this._response.Content.ReadFromJsonAsync<MemberProfileResponse>(JsonOptions);
        }
        else
        {
            testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(JsonOptions);
        }
    }

    private async Task PutChangePasswordAsync(string? cookie, string currentPassword, string newPassword, string confirmPassword)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/member/password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(currentPassword, newPassword, confirmPassword)),
        };
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        this._response = await testBase.Client.SendAsync(request);
        testBase.LastResponse = this._response;

        if (this._response.StatusCode == HttpStatusCode.OK)
        {
            if (this._response.Headers.TryGetValues("Set-Cookie", out var values))
            {
                this._reissuedSessionCookie = string.Join("; ", values).Split(';')[0].Trim();
            }
        }
        else
        {
            testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(JsonOptions);
        }
    }

    private async Task<string> LoginAndGetCookieAsync(string email, string password)
    {
        var request = new LoginRequest(email, password, null);
        using var response = await testBase.Client.PostAsJsonAsync("/api/v1/auth/login", request);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values));
        return string.Join("; ", values).Split(';')[0].Trim();
    }
}
