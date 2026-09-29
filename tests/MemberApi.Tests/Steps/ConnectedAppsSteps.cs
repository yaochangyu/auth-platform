using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using MemberApi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class ConnectedAppsSteps(PostgreSqlTestBase testBase, ScenarioContext scenarioContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpResponseMessage _response = null!;
    private ConnectedAppListResponse? _listResponse;

    [Given("該會員已授權應用程式「([^」]*)」")]
    public async Task Given該會員已授權應用程式(string appName)
    {
        var email = scenarioContext.Get<string>("email");
        var memberId = await this.GetMemberIdAsync(email);
        var grant = await ConnectedAppSeeder.SeedGrantAsync(testBase.Factory, memberId, appName);

        scenarioContext.Set(grant.Id, "grantId");
        scenarioContext.Set(grant.Id, $"grant:{appName}");
    }

    [Given("另一位會員已授權應用程式「([^」]*)」")]
    public async Task Given另一位會員已授權應用程式(string appName)
    {
        var otherEmail = $"connected-apps-other-{Guid.NewGuid():N}@1111.com.tw";
        var otherMember = await MemberSeeder.SeedMemberAsync(testBase.Factory, otherEmail, MemberStatus.Active);
        var grant = await ConnectedAppSeeder.SeedGrantAsync(testBase.Factory, otherMember.Id, appName);

        scenarioContext.Set(grant.Id, $"grant:{appName}");
    }

    [When("使用者攜帶該 Session Cookie 呼叫取得已連結應用程式清單 API")]
    public async Task When使用者攜帶該SessionCookie呼叫取得已連結應用程式清單Api()
    {
        var cookie = scenarioContext.Get<string>("sessionCookie");
        await this.GetListAsync(cookie);
    }

    [When("使用者未攜帶 Session Cookie 呼叫取得已連結應用程式清單 API")]
    public async Task When使用者未攜帶SessionCookie呼叫取得已連結應用程式清單Api()
    {
        await this.GetListAsync(null);
    }

    [Then("回應內容應包含一筆應用程式「([^」]*)」的授權紀錄")]
    public void Then回應內容應包含一筆應用程式的授權紀錄(string appName)
    {
        Assert.NotNull(this._listResponse);
        Assert.Contains(this._listResponse!.Items, item => item.AppName == appName);
    }

    [Then("回應內容的 totalCount 應為 (\\d+)")]
    public void Then回應內容的TotalCount應為(int expected)
    {
        Assert.NotNull(this._listResponse);
        Assert.Equal(expected, this._listResponse!.TotalCount);
    }

    [When("使用者攜帶該 Session Cookie 撤銷該應用程式的授權")]
    public async Task When使用者攜帶該SessionCookie撤銷該應用程式的授權()
    {
        var cookie = scenarioContext.Get<string>("sessionCookie");
        var grantId = scenarioContext.Get<Guid>("grantId");
        await this.DeleteAsync(cookie, grantId);
    }

    [When("使用者攜帶該 Session Cookie 撤銷「([^」]*)」的授權")]
    public async Task When使用者攜帶該SessionCookie撤銷指定應用程式的授權(string appName)
    {
        var cookie = scenarioContext.Get<string>("sessionCookie");
        var grantId = scenarioContext.Get<Guid>($"grant:{appName}");
        await this.DeleteAsync(cookie, grantId);
    }

    [When("使用者未攜帶 Session Cookie 撤銷編號為隨機 UUID 的應用程式授權")]
    public async Task When使用者未攜帶SessionCookie撤銷編號為隨機Uuid的應用程式授權()
    {
        await this.DeleteAsync(null, Guid.NewGuid());
    }

    [When("使用者攜帶該 Session Cookie 撤銷編號為不存在的應用程式授權")]
    public async Task When使用者攜帶該SessionCookie撤銷編號為不存在的應用程式授權()
    {
        var cookie = scenarioContext.Get<string>("sessionCookie");
        await this.DeleteAsync(cookie, Guid.NewGuid());
    }

    [Then("再次查詢已連結應用程式清單應不再包含「([^」]*)」")]
    public async Task Then再次查詢已連結應用程式清單應不再包含(string appName)
    {
        var cookie = scenarioContext.Get<string>("sessionCookie");
        await this.GetListAsync(cookie);
        Assert.DoesNotContain(this._listResponse!.Items, item => item.AppName == appName);
    }

    [Then("該筆授權紀錄應被標記為已撤銷")]
    public async Task Then該筆授權紀錄應被標記為已撤銷()
    {
        var grantId = scenarioContext.Get<Guid>("grantId");
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var grant = await dbContext.MemberGrants.SingleAsync(g => g.Id == grantId);
        Assert.NotNull(grant.RevokedAt);
    }

    private async Task GetListAsync(string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/member/connected-apps");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        this._response = await testBase.Client.SendAsync(request);
        testBase.LastResponse = this._response;

        if (this._response.StatusCode == HttpStatusCode.OK)
        {
            this._listResponse = await this._response.Content.ReadFromJsonAsync<ConnectedAppListResponse>(JsonOptions);
        }
        else
        {
            testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(JsonOptions);
        }
    }

    private async Task DeleteAsync(string? cookie, Guid grantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/member/connected-apps/{grantId}");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        this._response = await testBase.Client.SendAsync(request);
        testBase.LastResponse = this._response;

        if (this._response.StatusCode != HttpStatusCode.NoContent)
        {
            testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(JsonOptions);
        }
    }

    private async Task<Guid> GetMemberIdAsync(string email)
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await dbContext.Members.SingleAsync(m => m.Email == email);
        return member.Id;
    }
}
