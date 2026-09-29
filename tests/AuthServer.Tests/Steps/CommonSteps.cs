using AuthServer.Tests.Support;
using Reqnroll;

namespace AuthServer.Tests.Steps;

[Binding]
public class CommonSteps(AuthServerTestBase testBase)
{
    [Given("初始化 Auth Server 測試伺服器")]
    public void Given初始化AuthServer測試伺服器() => testBase.Start();

    [AfterScenario]
    public async Task 釋放測試伺服器()
    {
        await testBase.StopAsync();
        testBase.DeleteKeyDirectory();
    }

    [Then("回應狀態碼應為 (\\d+)")]
    public void Then回應狀態碼應為(int expected) => Assert.Equal(expected, (int)testBase.LastResponse!.StatusCode);
}
