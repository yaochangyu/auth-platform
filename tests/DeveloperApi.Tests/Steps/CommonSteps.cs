using DeveloperApi.Tests.Support;
using Reqnroll;

namespace DeveloperApi.Tests.Steps;

[Binding]
public class CommonSteps(DeveloperApiTestBase testBase)
{
    [Given("初始化 Developer API 測試伺服器")]
    public void Given初始化測試伺服器() => testBase.Start();

    [AfterScenario]
    public Task 釋放測試伺服器() => testBase.StopAsync();

    [Then("回應狀態碼應為 (\\d+)")]
    public void Then回應狀態碼應為(int expected) => Assert.Equal(expected, (int)testBase.LastResponse!.StatusCode);

    [Then("回應應為 Problem Details")]
    public void Then回應應為ProblemDetails() =>
        Assert.Equal("application/problem+json", testBase.LastResponse!.Content.Headers.ContentType?.MediaType);
}
