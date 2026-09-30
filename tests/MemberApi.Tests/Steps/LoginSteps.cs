using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using MemberApi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class LoginSteps(PostgreSqlTestBase testBase, ScenarioContext scenarioContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private HttpResponseMessage _response = null!;
    private LoginResponse? _loginResponse;
    private string? _sessionCookie;

    [Given("系統已存在一筆狀態為 Active 的會員，Email 為 \"([^\"]*)\"，密碼為 \"([^\"]*)\"")]
    public async Task Given系統已存在一筆狀態為Active的會員EmailPassword(string email, string password)
    {
        await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberStatus.Active, password);
        this.RememberCredentials(email, password);
    }

    [Given("系統已存在一筆狀態為 Pending 的會員，Email 為 \"([^\"]*)\"，密碼為 \"([^\"]*)\"")]
    public async Task Given系統已存在一筆狀態為Pending的會員EmailPassword(string email, string password)
    {
        await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberStatus.Pending, password);
        this.RememberCredentials(email, password);
    }

    [Given("系統已存在一筆狀態為 Suspended 的會員，Email 為 \"([^\"]*)\"，密碼為 \"([^\"]*)\"")]
    public async Task Given系統已存在一筆狀態為Suspended的會員EmailPassword(string email, string password)
    {
        await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberStatus.Suspended, password);
        this.RememberCredentials(email, password);
    }

    // 讓其他 Steps 類別（例如 LockoutSteps）能在同一情境內取得目前操作對象的帳密，不需重複宣告相同的 Given 文字。
    private void RememberCredentials(string email, string password)
    {
        scenarioContext.Set(email, "email");
        scenarioContext.Set(password, "password");
    }

    [Given("該會員已綁定手機號碼 \"([^\"]*)\"")]
    public async Task Given該會員已綁定手機號碼(string phoneNumber)
    {
        var email = scenarioContext.Get<string>("email");
        using var scope = testBase.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await db.Members.FirstAsync(m => m.Email == email);
        member.PhoneNumber = phoneNumber;
        member.PhoneVerifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    [When("使用者以 Email \"([^\"]*)\" 密碼 \"([^\"]*)\" 呼叫登入 API")]
    public async Task When使用者以EmailPassword呼叫登入Api(string email, string password)
    {
        await this.PostLoginAsync(email, password, null);
    }

    [When("使用者以手機號碼 \"([^\"]*)\" 密碼 \"([^\"]*)\" 呼叫登入 API")]
    public async Task When使用者以手機號碼密碼呼叫登入Api(string phone, string password)
    {
        await this.PostLoginAsync(phone, password, null);
    }

    [When("使用者以 Email \"([^\"]*)\" 密碼 \"([^\"]*)\" 並攜帶 returnUrl \"([^\"]*)\" 呼叫登入 API")]
    public async Task When使用者以EmailPasswordReturnUrl呼叫登入Api(string email, string password, string returnUrl)
    {
        await this.PostLoginAsync(email, password, returnUrl);
    }

    [Then("回應內容的 email 應為 \"([^\"]*)\"")]
    public void Then回應內容的email應為(string expectedEmail)
    {
        Assert.NotNull(this._loginResponse);
        Assert.Equal(expectedEmail, this._loginResponse.Email);
    }

    [Then("回應標頭 Set-Cookie 應包含 \"([^\"]*)\"")]
    public void Then回應標頭SetCookie應包含(string expectedFragment)
    {
        Assert.Contains(expectedFragment, GetSetCookieHeader(testBase.LastResponse!), StringComparison.OrdinalIgnoreCase);
    }

    [Then("回應標頭 Set-Cookie 應包含過期時間 \"([^\"]*)\"")]
    public void Then回應標頭SetCookie應包含過期時間(string expectedFragment)
    {
        Assert.Contains(expectedFragment, GetSetCookieHeader(testBase.LastResponse!), StringComparison.OrdinalIgnoreCase);
    }

    [Then("回應不應包含 Set-Cookie 標頭")]
    public void Then回應不應包含SetCookie標頭()
    {
        Assert.NotNull(testBase.LastResponse);
        Assert.False(testBase.LastResponse!.Headers.Contains("Set-Cookie"));
    }

    [Then("回應內容的 returnUrl 應為 \"([^\"]*)\"")]
    public void Then回應內容的ReturnUrl應為(string returnUrl)
    {
        Assert.NotNull(this._loginResponse);
        Assert.Equal(returnUrl, this._loginResponse!.ReturnUrl);
    }

    [Given("使用者已成功登入並取得有效的 Session Cookie")]
    public async Task Given使用者已成功登入並取得有效的SessionCookie()
    {
        // ponytail: 每次呼叫都用唯一 Email，避免被其他情境（甚至其他 Feature、可能並行執行）
        // 共用同一筆會員時，密碼或 SecurityStamp 被異動而污染這個情境的前置假設。
        var email = $"login-logout-{Guid.NewGuid():N}@1111.com.tw";
        const string password = "P@ssw0rd2026!";
        await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberStatus.Active, password);
        await this.PostLoginAsync(email, password, null);
        this._sessionCookie = GetSetCookieHeader(this._response).Split(';')[0].Trim();

        this.RememberCredentials(email, password);
        scenarioContext.Set(this._sessionCookie, "sessionCookie");
    }

    [When("使用者攜帶該 Session Cookie 呼叫登出 API")]
    public async Task When使用者攜帶該SessionCookie呼叫登出Api()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", this._sessionCookie);
        this._response = await testBase.Client.SendAsync(request);
        testBase.LastResponse = this._response;
    }

    [When("使用者未攜帶 Session Cookie 呼叫登出 API")]
    public async Task When使用者未攜帶SessionCookie呼叫登出Api()
    {
        this._response = await testBase.Client.PostAsync("/api/v1/auth/logout", content: null);
        testBase.LastResponse = this._response;
        testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<MemberApi.Tests.Support.ProblemDetailsPayload>(JsonOptions);
    }

    private async Task PostLoginAsync(string email, string password, string? returnUrl)
    {
        var request = new LoginRequest(email, password, returnUrl);
        this._response = await testBase.Client.PostAsJsonAsync("/api/v1/auth/login", request);
        testBase.LastResponse = this._response;

        if (this._response.StatusCode == HttpStatusCode.OK)
        {
            this._loginResponse = await this._response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        }
        else
        {
            testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<MemberApi.Tests.Support.ProblemDetailsPayload>(JsonOptions);
        }
    }

    private static string GetSetCookieHeader(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values));
        return string.Join("; ", values!);
    }
}
