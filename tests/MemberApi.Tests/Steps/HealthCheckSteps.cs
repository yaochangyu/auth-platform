using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemberApi.Contracts;
using MemberApi.Tests.Support;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class HealthCheckSteps(PostgreSqlTestBase testBase)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgreSqlTestBase _testBase = testBase;
    private HttpResponseMessage _response = null!;
    private HealthResponse? _healthResponse;

    [When("使用者呼叫 GET \\/health 端點")]
    public async Task When使用者呼叫Get健康檢查端點()
    {
        this._response = await this._testBase.Client.GetAsync("/health");
        this._testBase.LastResponse = this._response;
        this._healthResponse = await this._response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions);
    }

    [Then("回應內容的 status 欄位應為 \"Healthy\"")]
    public void Then回應內容的Status欄位應為Healthy()
    {
        Assert.NotNull(this._healthResponse);
        Assert.Equal(HealthStatus.Healthy, this._healthResponse!.Status);
    }

    [Then("回應內容應包含 timestamp 與 version 欄位")]
    public void Then回應內容應包含TimestampVersion欄位()
    {
        Assert.NotNull(this._healthResponse);
        Assert.NotEqual(default, this._healthResponse!.Timestamp);
        Assert.False(string.IsNullOrWhiteSpace(this._healthResponse.Version));
    }

    [Then("回應內容的 checks 應包含名稱為 \"PostgreSQL\" 且 status 為 \"Healthy\" 的檢測項目")]
    public void Then回應內容的Checks應包含PostgreSql檢測項目()
    {
        Assert.NotNull(this._healthResponse);
        Assert.Contains(
            this._healthResponse!.Checks,
            item => item.Component == "PostgreSQL" && item.Status == HealthCheckStatus.Healthy);
    }
}
