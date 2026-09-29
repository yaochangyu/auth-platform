using AdminApi.Tests.Support;
using Reqnroll;

namespace AdminApi.Tests.Steps;

[Binding]
public class CommonSteps(AdminApiTestBase testBase)
{
    [Given("初始化 Admin API 測試伺服器")]
    public void Given初始化() => testBase.Start();

    [AfterScenario]
    public Task 釋放() => testBase.StopAsync();

    [Then("回應狀態碼應為 (\\d+)")]
    public void Then回應狀態碼(int expected) => Assert.Equal(expected, (int)testBase.LastResponse!.StatusCode);

    [Then("回應應為 Problem Details")]
    public void Then回應應為ProblemDetails() =>
        Assert.Equal("application/problem+json", testBase.LastResponse!.Content.Headers.ContentType?.MediaType);

    [Then("回應的錯誤應指出欄位 \"([^\"]*)\"")]
    public void Then錯誤欄位(string field) => Assert.True(testBase.LastBody.GetProperty("errors").TryGetProperty(field, out _), $"errors 應包含欄位 {field}");
}
