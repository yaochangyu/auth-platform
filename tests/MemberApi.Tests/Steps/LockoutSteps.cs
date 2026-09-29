using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemberApi.Contracts;
using MemberApi.Infrastructure.Persistence;
using MemberApi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class LockoutSteps(PostgreSqlTestBase testBase, ScenarioContext scenarioContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const string WrongPassword = "WrongPassword!1";

    private HttpResponseMessage _response = null!;
    private readonly List<HttpStatusCode> _attemptStatusCodes = [];

    [Given("系統已存在一筆狀態為 Active 且目前處於鎖定中的會員，Email 為 \"([^\"]*)\"，密碼為 \"([^\"]*)\"")]
    public async Task Given系統已存在一筆狀態為Active且目前處於鎖定中的會員(string email, string password)
    {
        var member = await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberApi.Entities.MemberStatus.Active, password);
        await this.SetLockoutStateAsync(member.Id, failedAttempts: 5, lockoutEndAt: testBase.TimeProvider.GetUtcNow() + LockoutDuration);

        scenarioContext.Set(email, "email");
        scenarioContext.Set(password, "password");
    }

    [Given("系統已存在一筆狀態為 Active 且鎖定時效已過期的會員，Email 為 \"([^\"]*)\"，密碼為 \"([^\"]*)\"")]
    public async Task Given系統已存在一筆狀態為Active且鎖定時效已過期的會員(string email, string password)
    {
        var member = await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberApi.Entities.MemberStatus.Active, password);
        await this.SetLockoutStateAsync(member.Id, failedAttempts: 5, lockoutEndAt: testBase.TimeProvider.GetUtcNow() + LockoutDuration);

        // 利用 TimeProvider 推進時間至鎖定期滿之後，驗證系統依實際時鐘判斷解鎖，而非直接寫入一個過去的時間戳記。
        testBase.TimeProvider.Advance(LockoutDuration + TimeSpan.FromMinutes(1));

        scenarioContext.Set(email, "email");
        scenarioContext.Set(password, "password");
    }

    [Given("該會員已連續輸入錯誤密碼 (\\d+) 次")]
    public async Task Given該會員已連續輸入錯誤密碼次(int times)
    {
        var email = scenarioContext.Get<string>("email");
        for (var i = 0; i < times; i++)
        {
            await this.PostLoginAsync(email, WrongPassword);
        }
    }

    [When("使用者以錯誤密碼連續呼叫登入 API \"(\\d+)\" 次")]
    public async Task When使用者以錯誤密碼連續呼叫登入Api次(int times)
    {
        var email = scenarioContext.Get<string>("email");
        this._attemptStatusCodes.Clear();
        for (var i = 0; i < times; i++)
        {
            await this.PostLoginAsync(email, WrongPassword);
            this._attemptStatusCodes.Add(this._response.StatusCode);
        }
    }

    [When("使用者以錯誤密碼呼叫登入 API")]
    public async Task When使用者以錯誤密碼呼叫登入Api()
    {
        var email = scenarioContext.Get<string>("email");
        await this.PostLoginAsync(email, WrongPassword);
    }

    [When("使用者以正確密碼呼叫登入 API")]
    public async Task When使用者以正確密碼呼叫登入Api()
    {
        var email = scenarioContext.Get<string>("email");
        var password = scenarioContext.Get<string>("password");
        await this.PostLoginAsync(email, password);
    }

    [Then("每次回應狀態碼皆為 (\\d+)")]
    public void Then每次回應狀態碼皆為(int expectedStatusCode)
    {
        Assert.NotEmpty(this._attemptStatusCodes);
        Assert.All(this._attemptStatusCodes, code => Assert.Equal(expectedStatusCode, (int)code));
    }

    [Then("該會員的 failedLoginAttempts 應為 (\\d+)")]
    public async Task Then該會員的FailedLoginAttempts應為(int expected)
    {
        var email = scenarioContext.Get<string>("email");
        Assert.Equal(expected, await this.GetFailedLoginAttemptsAsync(email));
    }

    [Then("該會員不應處於鎖定狀態")]
    public async Task Then該會員不應處於鎖定狀態()
    {
        var email = scenarioContext.Get<string>("email");
        Assert.Null(await this.GetLockoutEndAtAsync(email));
    }

    [Then("該會員的 lockoutEndAt 應為空")]
    public async Task Then該會員的LockoutEndAt應為空()
    {
        var email = scenarioContext.Get<string>("email");
        Assert.Null(await this.GetLockoutEndAtAsync(email));
    }

    [Then("回應內容的 failedLoginAttempts 應為 (\\d+)")]
    public void Then回應內容的FailedLoginAttempts應為(int expected)
    {
        Assert.NotNull(testBase.LastProblemDetails);
        Assert.Equal(expected, testBase.LastProblemDetails!.FailedLoginAttempts);
    }

    [Then("回應內容的 lockoutEndAt 應為 15 分鐘後的 UTC 時間戳記")]
    public void Then回應內容的LockoutEndAt應為15分鐘後的Utc時間戳記()
    {
        Assert.NotNull(testBase.LastProblemDetails?.LockoutEndAt);
        var expected = testBase.TimeProvider.GetUtcNow() + LockoutDuration;
        var actual = testBase.LastProblemDetails!.LockoutEndAt!.Value;
        Assert.True(Math.Abs((actual - expected).TotalSeconds) < 5, $"lockoutEndAt {actual:O} 與預期 {expected:O} 差距過大");
    }

    [Then("回應內容不應包含 failedLoginAttempts 或 lockoutEndAt 欄位")]
    public void Then回應內容不應包含FailedLoginAttempts或LockoutEndAt欄位()
    {
        Assert.NotNull(testBase.LastProblemDetails);
        Assert.Null(testBase.LastProblemDetails!.FailedLoginAttempts);
        Assert.Null(testBase.LastProblemDetails.LockoutEndAt);
    }

    private async Task PostLoginAsync(string email, string password)
    {
        var request = new LoginRequest(email, password, null);
        this._response = await testBase.Client.PostAsJsonAsync("/api/v1/auth/login", request);
        testBase.LastResponse = this._response;
        testBase.LastProblemDetails = this._response.StatusCode == HttpStatusCode.OK
            ? null
            : await this._response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(JsonOptions);
    }

    private async Task SetLockoutStateAsync(Guid memberId, int failedAttempts, DateTimeOffset? lockoutEndAt)
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await dbContext.Members.SingleAsync(m => m.Id == memberId);
        member.FailedLoginAttempts = failedAttempts;
        member.LockoutEndAt = lockoutEndAt;
        await dbContext.SaveChangesAsync();
    }

    private async Task<int> GetFailedLoginAttemptsAsync(string email)
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await dbContext.Members.SingleAsync(m => m.Email == email);
        return member.FailedLoginAttempts;
    }

    private async Task<DateTimeOffset?> GetLockoutEndAtAsync(string email)
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await dbContext.Members.SingleAsync(m => m.Email == email);
        return member.LockoutEndAt;
    }
}
