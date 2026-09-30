using System.Net;
using System.Net.Http.Json;
using MemberApi.Contracts;
using MemberApi.Tests.Support;
using Reqnroll;
using Xunit;

namespace MemberApi.Tests.Steps;

[Binding]
public class RateLimitingSteps(PostgreSqlTestBase testBase)
{
    private string _currentIp = "127.0.0.1";
    private readonly List<HttpResponseMessage> _previousResponses = [];

    [Given("來自客戶端 IP \"([^\"]*)\"")]
    public void Given來自客戶端Ip(string ip)
    {
        this._currentIp = ip;
        this._previousResponses.Clear();
    }

    [When("該客戶端快速連續呼叫註冊 API (\\d+) 次")]
    public async Task When該客戶端快速連續呼叫註冊Api次(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var request = CreateRegisterRequest(this._currentIp, $"ratelimit-{i}");
            var response = await testBase.SendAndRecordAsync(request);
            this._previousResponses.Add(response);
        }
    }

    [When("該客戶端快速連續呼叫忘記密碼 API (\\d+) 次")]
    public async Task When該客戶端快速連續呼叫忘記密碼Api次(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var request = CreateForgotPasswordRequest(this._currentIp, $"forgot-{i}");
            var response = await testBase.SendAndRecordAsync(request);
            this._previousResponses.Add(response);
        }
    }

    [Then("所有前置請求皆應獲得非 429 的回應")]
    public void Then所有前置請求皆應獲得非429的回應()
    {
        Assert.NotEmpty(this._previousResponses);
        foreach (var response in this._previousResponses)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [When("該客戶端再次呼叫註冊 API")]
    public async Task When該客戶端再次呼叫註冊Api()
    {
        var request = CreateRegisterRequest(this._currentIp, "ratelimit-next");
        await testBase.SendAndRecordAsync(request);
    }

    [When("該客戶端再次呼叫忘記密碼 API")]
    public async Task When該客戶端再次呼叫忘記密碼Api()
    {
        var request = CreateForgotPasswordRequest(this._currentIp, "forgot-next");
        await testBase.SendAndRecordAsync(request);
    }

    [When("另一來自客戶端 IP \"([^\"]*)\" 呼叫註冊 API")]
    public async Task When另一來自客戶端Ip呼叫註冊Api(string ip)
    {
        var request = CreateRegisterRequest(ip, "other-ip");
        await testBase.SendAndRecordAsync(request);
    }

    [When("該客戶端呼叫忘記密碼 API")]
    public async Task When該客戶端呼叫忘記密碼Api()
    {
        var request = CreateForgotPasswordRequest(this._currentIp, "forgot-diff-endpoint");
        await testBase.SendAndRecordAsync(request);
    }

    [Then("回應狀態碼應為非 429")]
    public void Then回應狀態碼應為非429()
    {
        Assert.NotNull(testBase.LastResponse);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, testBase.LastResponse!.StatusCode);
    }

    [Then("回應標頭應包含 \"([^\"]*)\"")]
    public void Then回應標頭應包含(string headerName)
    {
        Assert.NotNull(testBase.LastResponse);
        Assert.True(
            testBase.LastResponse!.Headers.Contains(headerName) || testBase.LastResponse.Content.Headers.Contains(headerName),
            $"回應標頭未包含預期的 '{headerName}'");
    }

    private static HttpRequestMessage CreateRegisterRequest(string clientIp, string emailPrefix)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new RegisterRequest(
                $"{emailPrefix}-{Guid.NewGuid()}@1111.com.tw",
                "P@ssw0rd2026!",
                "P@ssw0rd2026!",
                "限速測試"
            )),
        };
        request.Headers.Add("X-Forwarded-For", clientIp);
        return request;
    }

    private static HttpRequestMessage CreateForgotPasswordRequest(string clientIp, string emailPrefix)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/forgot-password")
        {
            Content = JsonContent.Create(new ForgotPasswordRequest(
                $"{emailPrefix}-{Guid.NewGuid()}@1111.com.tw"
            )),
        };
        request.Headers.Add("X-Forwarded-For", clientIp);
        return request;
    }
}
