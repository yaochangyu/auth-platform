using MemberApi.Tests.Support;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class CommonSteps(PostgreSqlTestBase testBase)
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
}
