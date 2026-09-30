using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemberApi.Contracts;
using MemberApi.Tests.Support;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class FirstPartyAccountIsolationSteps(PostgreSqlTestBase testBase, ScenarioContext scenarioContext)
{

    [When("客戶端於 Authorization 標頭攜帶該 Bearer Token 嘗試呼叫修改密碼 API")]
    public async Task When客戶端於Authorization標頭攜帶該BearerToken嘗試呼叫修改密碼Api()
    {
        var token = scenarioContext.Get<string>("bearerToken");
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/member/password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest("OldPassword123!", "NewPassword456!", "NewPassword456!")),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await testBase.SendAndRecordAsync(request);
    }

    [When("客戶端於 Authorization 標頭攜帶該 Bearer Token 嘗試呼叫查詢已連結應用程式清單 API")]
    public async Task When客戶端於Authorization標頭攜帶該BearerToken嘗試呼叫查詢已連結應用程式清單Api()
    {
        var token = scenarioContext.Get<string>("bearerToken");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/member/connected-apps");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await testBase.SendAndRecordAsync(request);
    }

    [When("客戶端於 Authorization 標頭攜帶該 Bearer Token 嘗試呼叫撤銷已連結應用程式 API")]
    public async Task When客戶端於Authorization標頭攜帶該BearerToken嘗試呼叫撤銷已連結應用程式Api()
    {
        var token = scenarioContext.Get<string>("bearerToken");
        var randomAppId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/member/connected-apps/{randomAppId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await testBase.SendAndRecordAsync(request);
    }
}
