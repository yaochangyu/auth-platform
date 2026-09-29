using MemberApi.Infrastructure.Persistence;
using MemberApi.Security;
using MemberApi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class CommonSteps(PostgreSqlTestBase testBase, ScenarioContext scenarioContext)
{
    [Given("初始化測試伺服器")]
    public Task Given初始化測試伺服器()
    {
        return testBase.StartAsync();
    }

    [AfterScenario]
    public Task 釋放測試伺服器()
    {
        return testBase.StopAsync();
    }

    [Then("回應狀態碼應為 (\\d+)")]
    public void Then回應狀態碼應為(int expectedStatusCode)
    {
        Assert.NotNull(testBase.LastResponse);
        Assert.Equal(expectedStatusCode, (int)testBase.LastResponse!.StatusCode);
    }

    [Then("回應內容應為符合 RFC 7807 的驗證錯誤 Problem Details")]
    public void Then回應內容應為符合Rfc7807的驗證錯誤ProblemDetails()
    {
        Assert.NotNull(testBase.LastProblemDetails);
        Assert.False(string.IsNullOrWhiteSpace(testBase.LastProblemDetails!.Type));
        Assert.False(string.IsNullOrWhiteSpace(testBase.LastProblemDetails.Title));
        Assert.Equal((int)testBase.LastResponse!.StatusCode, testBase.LastProblemDetails.Status);
    }

    [Then("回應內容應為符合 RFC 7807 的 Problem Details 錯誤")]
    public void Then回應內容應為符合Rfc7807的ProblemDetails錯誤()
    {
        this.Then回應內容應為符合Rfc7807的驗證錯誤ProblemDetails();
    }

    [Then("回應內容應為符合 RFC 7807 的 LockoutProblemDetails")]
    public void Then回應內容應為符合Rfc7807的LockoutProblemDetails()
    {
        this.Then回應內容應為符合Rfc7807的驗證錯誤ProblemDetails();
        Assert.NotNull(testBase.LastProblemDetails!.FailedLoginAttempts);
        Assert.NotNull(testBase.LastProblemDetails.LockoutEndAt);
    }

    [Then("錯誤類型應為 \"([^\"]*)\"")]
    public void Then錯誤類型應為(string expectedType)
    {
        Assert.NotNull(testBase.LastProblemDetails);
        Assert.Equal(expectedType, testBase.LastProblemDetails!.Type);
    }

    // 供 Registration/ResetPassword 等多個 Feature 共用：斷言「目前操作中的驗證權杖」已被標記為已使用。
    // 呼叫端須在使用驗證權杖的 When/Given 步驟中，先以 scenarioContext.Set(token, "verificationToken") 記錄下來。
    [Then("該驗證權杖應被標記為已使用")]
    public async Task Then該驗證權杖應被標記為已使用()
    {
        var token = scenarioContext.Get<string>("verificationToken");
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var tokenHash = VerificationTokenHasher.Hash(token);
        var entity = await dbContext.VerificationTokens.SingleAsync(t => t.TokenHash == tokenHash);
        Assert.NotNull(entity.UsedAt);
    }
}
