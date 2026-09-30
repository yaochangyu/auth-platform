using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using MemberApi.Sms;
using MemberApi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class SmsOtpSteps(PostgreSqlTestBase testBase, ScenarioContext scenarioContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private HttpResponseMessage _response = null!;
    private SendSmsOtpResponse? _sendResponse;
    private VerifyPhoneResponse? _verifyResponse;

    [When("請求發送簡訊驗證碼至手機 \"([^\"]*)\"")]
    public async Task When請求發送簡訊驗證碼至手機(string phoneNumber)
    {
        var request = new SendSmsOtpRequest(phoneNumber);
        this._response = await testBase.Client.PostAsJsonAsync("/api/v1/auth/send-sms-otp", request);
        testBase.LastResponse = this._response;

        if (this._response.StatusCode == HttpStatusCode.OK)
        {
            this._sendResponse = await this._response.Content.ReadFromJsonAsync<SendSmsOtpResponse>(JsonOptions);
        }
        else
        {
            testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(JsonOptions);
        }
    }

    [Given("已發送簡訊驗證碼至手機 \"([^\"]*)\"")]
    public async Task Given已發送簡訊驗證碼至手機(string phoneNumber)
    {
        await this.When請求發送簡訊驗證碼至手機(phoneNumber);
    }

    [When("再次請求發送簡訊驗證碼至手機 \"([^\"]*)\"")]
    public async Task When再次請求發送簡訊驗證碼至手機(string phoneNumber)
    {
        await this.When請求發送簡訊驗證碼至手機(phoneNumber);
    }

    [Then("回應內容應包含訊息 \"([^\"]*)\"")]
    public void Then回應內容應包含訊息(string message)
    {
        Assert.NotNull(this._sendResponse);
        Assert.Contains(message, this._sendResponse.Message);
    }

    [Then("回應內容之冷卻時間應為 {int} 秒")]
    public void Then回應內容之冷卻時間應為秒(int seconds)
    {
        Assert.NotNull(this._sendResponse);
        Assert.Equal(seconds, this._sendResponse.RetryAfterSeconds);
    }

    [Given("已發送簡訊驗證碼至手機 \"([^\"]*)\" 並取得驗證碼")]
    [When("已發送簡訊驗證碼至手機 \"([^\"]*)\" 並取得驗證碼")]
    public async Task Given已發送簡訊驗證碼至手機並取得驗證碼(string phoneNumber)
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var otpService = scope.ServiceProvider.GetRequiredService<ISmsOtpService>();
        var generated = await otpService.GenerateOtpAsync(phoneNumber, SmsOtpPurpose.PhoneVerification);
        scenarioContext[$"otp_{phoneNumber}"] = generated.Code;
    }

    [When("使用正確的驗證碼驗證手機 \"([^\"]*)\"")]
    public async Task When使用正確的驗證碼驗證手機(string phoneNumber)
    {
        var code = scenarioContext.Get<string>($"otp_{phoneNumber}");
        var requestPayload = new VerifyPhoneRequest(phoneNumber, code);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/verify-phone");
        httpRequest.Content = JsonContent.Create(requestPayload);

        if (testBase.LastResponse?.Headers.TryGetValues("Set-Cookie", out var cookieValues) == true)
        {
            var cookie = cookieValues.First().Split(';')[0].Trim();
            httpRequest.Headers.Add("Cookie", cookie);
        }
        else if (scenarioContext.TryGetValue<string>("sessionCookie", out var cookie))
        {
            httpRequest.Headers.Add("Cookie", cookie);
        }

        this._response = await testBase.Client.SendAsync(httpRequest);
        testBase.LastResponse = this._response;

        if (this._response.StatusCode == HttpStatusCode.OK)
        {
            this._verifyResponse = await this._response.Content.ReadFromJsonAsync<VerifyPhoneResponse>(JsonOptions);
        }
        else
        {
            testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(JsonOptions);
        }
    }

    [When("再次輸入正確的驗證碼驗證手機 \"([^\"]*)\"")]
    public async Task When再次輸入正確的驗證碼驗證手機(string phoneNumber)
    {
        await this.When使用正確的驗證碼驗證手機(phoneNumber);
    }

    [When("使用錯誤的驗證碼 \"([^\"]*)\" 驗證手機 \"([^\"]*)\"")]
    public async Task When使用錯誤的驗證碼驗證手機(string code, string phoneNumber)
    {
        var request = new VerifyPhoneRequest(phoneNumber, code);
        this._response = await testBase.Client.PostAsJsonAsync("/api/v1/auth/verify-phone", request);
        testBase.LastResponse = this._response;
        testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(JsonOptions);
    }

    [When(@"連續輸入 (\d+) 次錯誤驗證碼驗證手機 ""([^""]*)""")]
    public async Task When連續輸入次錯誤驗證碼驗證手機(int times, string phoneNumber)
    {
        for (var i = 1; i <= times; i++)
        {
            var request = new VerifyPhoneRequest(phoneNumber, "000000");
            this._response = await testBase.Client.PostAsJsonAsync("/api/v1/auth/verify-phone", request);
            testBase.LastResponse = this._response;
        }

        testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(JsonOptions);
    }

    [Then("回應內容應包含欄位驗證錯誤")]
    public void Then回應內容應包含欄位驗證錯誤()
    {
        Assert.NotNull(testBase.LastProblemDetails);
        Assert.NotNull(testBase.LastProblemDetails.Errors);
        Assert.NotEmpty(testBase.LastProblemDetails.Errors);
    }

    [Then("第 {int} 次回應狀態碼應為 {int}")]
    public void Then第次回應狀態碼應為(int times, int statusCode)
    {
        Assert.Equal(statusCode, (int)this._response.StatusCode);
    }

    [Given("該驗證碼已過期")]
    public async Task Given該驗證碼已過期()
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var latest = await dbContext.SmsOtps.OrderByDescending(x => x.CreatedAt).FirstAsync();
        latest.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();
    }

    [Then("資料庫中該會員的手助號碼應為 \"([^\"]*)\" 且已標記驗證時間")]
    [Then("資料庫中該會員的手機號碼應為 \"([^\"]*)\" 且已標記驗證時間")]
    public async Task Then資料庫中該會員的手機號碼應為且已標記驗證時間(string phoneNumber)
    {
        var email = scenarioContext.Get<string>("email");
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await dbContext.Members.SingleAsync(m => m.Email == email);
        Assert.Equal(phoneNumber, member.PhoneNumber);
        Assert.NotNull(member.PhoneVerifiedAt);
    }

    [Then("回應內容應包含成功訊息 \"([^\"]*)\"")]
    public void Then回應內容應包含成功訊息(string message)
    {
        Assert.NotNull(this._verifyResponse);
        Assert.True(this._verifyResponse.Success);
        Assert.Contains(message, this._verifyResponse.Message);
    }

    [Then("回應內容應包含 \"([^\"]*)\"")]
    public void Then回應內容應包含(string titleOrDetail)
    {
        Assert.NotNull(testBase.LastProblemDetails);
        var combined = $"{testBase.LastProblemDetails.Title} {testBase.LastProblemDetails.Detail}";
        Assert.Contains(titleOrDetail, combined);
    }

    [Then("回應內容應提示剩餘嘗試次數")]
    public void Then回應內容應提示剩餘嘗試次數()
    {
        Assert.NotNull(testBase.LastProblemDetails);
        var combined = $"{testBase.LastProblemDetails.Title} {testBase.LastProblemDetails.Detail}";
        Assert.Contains("剩餘嘗試次數", combined);
    }
}
