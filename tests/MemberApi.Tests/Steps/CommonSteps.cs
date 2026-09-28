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
}
