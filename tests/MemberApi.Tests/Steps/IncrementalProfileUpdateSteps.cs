using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using MemberApi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class IncrementalProfileUpdateSteps(PostgreSqlTestBase testBase, ScenarioContext scenarioContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private Member? _currentMember;
    private MemberProfileResponse? _profileResponse;

    [Given("資料庫中存在一名尚未填寫生日與擴充屬性之已啟用會員")]
    public async Task Given資料庫中存在一名尚未填寫生日與擴充屬性之已啟用會員()
    {
        var email = $"profile_patch_{Guid.NewGuid():N}@example.com";
        this._currentMember = await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberStatus.Active);
        scenarioContext.Set(email, "email");
        scenarioContext.Set(this._currentMember, "member");
    }

    [Given("該會員已填寫學歷為 \"([^\"]*)\" 與職稱 \"([^\"]*)\"")]
    public async Task Given該會員已填寫學歷為與職稱(string education, string jobTitle)
    {
        Assert.NotNull(this._currentMember);
        using var scope = testBase.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await db.Members.FirstAsync(m => m.Id == this._currentMember!.Id);
        member.Education = education;
        member.JobTitle = jobTitle;
        await db.SaveChangesAsync();
    }

    [Given("該會員資料庫中生日已存在且為 \"([^\"]*)\"")]
    public async Task Given該會員資料庫中生日已存在且為(string birthdayText)
    {
        Assert.NotNull(this._currentMember);
        var birthday = DateOnly.Parse(birthdayText);
        using var scope = testBase.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await db.Members.FirstAsync(m => m.Id == this._currentMember!.Id);
        member.Birthday = birthday;
        await db.SaveChangesAsync();
    }

    [When("客戶端攜帶該 Token 呼叫補填個人檔案 API，填寫生日 \"([^\"]*)\"、學歷 \"([^\"]*)\"、地址 \"([^\"]*)\"、職稱 \"([^\"]*)\"")]
    public async Task When客戶端攜帶該Token呼叫補填個人檔案Api填寫生日學歷地址職稱(string birthday, string education, string address, string jobTitle)
    {
        var token = scenarioContext.Get<string>("bearerToken");
        var requestPayload = new UpdateMemberProfileRequest(
            Birthday: DateOnly.Parse(birthday),
            Education: education,
            Address: address,
            JobTitle: jobTitle);

        await this.SendPatchProfileAsync(requestPayload, bearerToken: token, cookie: null);
    }

    [When("客戶端攜帶該 Token 呼叫補填個人檔案 API，更新學歷為 \"([^\"]*)\"、職稱 \"([^\"]*)\"")]
    public async Task When客戶端攜帶該Token呼叫補填個人檔案Api更新學歷為職稱(string education, string jobTitle)
    {
        var token = scenarioContext.Get<string>("bearerToken");
        var requestPayload = new UpdateMemberProfileRequest(Education: education, JobTitle: jobTitle);

        await this.SendPatchProfileAsync(requestPayload, bearerToken: token, cookie: null);
    }

    [When("客戶端攜帶該 Token 呼叫補填個人檔案 API，嘗試覆寫生日為 \"([^\"]*)\"")]
    public async Task When客戶端攜帶該Token呼叫補填個人檔案Api嘗試覆寫生日為(string birthday)
    {
        var token = scenarioContext.Get<string>("bearerToken");
        var requestPayload = new UpdateMemberProfileRequest(Birthday: DateOnly.Parse(birthday));

        await this.SendPatchProfileAsync(requestPayload, bearerToken: token, cookie: null);
    }

    [When("使用者攜帶該 Session Cookie 呼叫補填個人檔案 API，填寫最高學歷為 \"([^\"]*)\"")]
    public async Task When使用者攜帶該SessionCookie呼叫補填個人檔案Api填寫最高學歷為(string education)
    {
        var sessionCookie = scenarioContext.Get<string>("sessionCookie");
        var requestPayload = new UpdateMemberProfileRequest(Education: education);

        await this.SendPatchProfileAsync(requestPayload, bearerToken: null, cookie: sessionCookie);
    }

    [When("客戶端攜帶該 Token 呼叫補填個人檔案 API，填寫最高學歷為 \"([^\"]*)\"")]
    public async Task When客戶端攜帶該Token呼叫補填個人檔案Api填寫最高學歷為(string education)
    {
        var token = scenarioContext.Get<string>("bearerToken");
        var requestPayload = new UpdateMemberProfileRequest(Education: education);

        await this.SendPatchProfileAsync(requestPayload, bearerToken: token, cookie: null);
    }

    [Then("回應內容應包含更新後的生日 \"([^\"]*)\"、學歷 \"([^\"]*)\"、地址 \"([^\"]*)\"、職稱 \"([^\"]*)\"")]
    public void Then回應內容應包含更新後的生日學歷地址職稱(string birthday, string education, string address, string jobTitle)
    {
        Assert.NotNull(this._profileResponse);
        Assert.Equal(DateOnly.Parse(birthday), this._profileResponse!.Birthday);
        Assert.Equal(education, this._profileResponse.Education);
        Assert.Equal(address, this._profileResponse.Address);
        Assert.Equal(jobTitle, this._profileResponse.JobTitle);
    }

    [Then("回應內容應包含更新後的學歷 \"([^\"]*)\" 與職稱 \"([^\"]*)\"")]
    public void Then回應內容應包含更新後的學歷與職稱(string education, string jobTitle)
    {
        Assert.NotNull(this._profileResponse);
        Assert.Equal(education, this._profileResponse!.Education);
        Assert.Equal(jobTitle, this._profileResponse.JobTitle);
    }

    [Then("回應內容應包含更新後的學歷 \"([^\"]*)\"")]
    public void Then回應內容應包含更新後的學歷(string education)
    {
        Assert.NotNull(this._profileResponse);
        Assert.Equal(education, this._profileResponse!.Education);
    }

    private async Task SendPatchProfileAsync(UpdateMemberProfileRequest payload, string? bearerToken, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/member/profile")
        {
            Content = JsonContent.Create(payload),
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        if (!string.IsNullOrWhiteSpace(cookie))
        {
            request.Headers.Add("Cookie", cookie);
        }

        var response = await testBase.SendAndRecordAsync(request);
        if (response.IsSuccessStatusCode)
        {
            this._profileResponse = await response.Content.ReadFromJsonAsync<MemberProfileResponse>(JsonOptions);
        }
    }
}
