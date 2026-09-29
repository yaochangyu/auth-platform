using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AuthServer.Tests.Support;
using Npgsql;
using Reqnroll;

namespace AuthServer.Tests.Steps;

[Binding]
public class UserInfoSteps(AuthServerTestBase testBase, AuthorizeSteps authorize, TokenSteps token)
{
    private JsonElement _claims;

    [When("以 Bearer Access Token 呼叫 GET \\/connect\\/userinfo")]
    public Task When以AccessToken呼叫() => this.CallAsync(token.AccessToken);

    [When("以 Bearer Access Token 呼叫 POST \\/connect\\/userinfo")]
    public Task When以AccessToken呼叫Post() => this.CallAsync(token.AccessToken, HttpMethod.Post);

    [When("以最初取得的 Access Token 呼叫 GET \\/connect\\/userinfo")]
    public Task When以最初AccessToken呼叫() => this.CallAsync(token.InitialAccessToken);

    [Given("該會員帳號已被刪除")]
    public async Task Given會員已被刪除()
    {
        await using var connection = new NpgsqlConnection(TestRunHooks.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "delete from members where id = @id";
        command.Parameters.AddWithValue("id", authorize.MemberId);
        await command.ExecuteNonQueryAsync();
    }

    [When("未帶 Access Token 呼叫 GET \\/connect\\/userinfo")]
    public Task When未帶Token呼叫() => this.CallAsync(null);

    [When("以偽造的 Access Token 呼叫 GET \\/connect\\/userinfo")]
    public Task When偽造Token呼叫() => this.CallAsync("forged.access.token");

    [When("以簽章被竄改的 Access Token 呼叫 GET \\/connect\\/userinfo")]
    public Task When竄改Token呼叫()
    {
        var original = token.AccessToken;
        var tampered = original[..^1] + (original[^1] == 'A' ? 'B' : 'A');
        return this.CallAsync(tampered);
    }

    [Then("回應應只包含欄位 \"(.*)\"")]
    public void Then只包含欄位(string fields) =>
        Assert.Equal(fields.Split('、').Order(), this._claims.EnumerateObject().Select(property => property.Name).Order());

    [Then("回應的 sub 應為該會員")]
    public void ThenSub() => Assert.Equal(authorize.MemberId.ToString(), this._claims.GetProperty("sub").GetString());

    [Then("回應的 email 應為會員的 Email")]
    public void ThenEmail() => Assert.Equal(AuthorizeSteps.MemberEmail, this._claims.GetProperty("email").GetString());

    [Then("回應的 email_verified 應為 true")]
    public void ThenEmailVerified() => Assert.True(this._claims.GetProperty("email_verified").GetBoolean());

    [Then("回應的 nickname 應為會員的顯示名稱")]
    public void ThenNickname() => Assert.Equal(AuthorizeSteps.MemberDisplayName, this._claims.GetProperty("nickname").GetString());

    [Then("回應的 updated_at 應為會員資料最後更新時間的 Unix 秒數")]
    public void ThenUpdatedAt()
    {
        // OIDC Core 5.1：updated_at 為 JSON 數字（自 1970-01-01 起的秒數），不是字串或 ISO 8601。
        var updatedAt = this._claims.GetProperty("updated_at");
        Assert.Equal(JsonValueKind.Number, updatedAt.ValueKind);
        Assert.Equal(AuthorizeSteps.MemberUpdatedAt.ToUnixTimeSeconds(), updatedAt.GetInt64());
    }

    [Then("回應應帶有 Cache-Control 為 \"(.*)\"")]
    public void ThenCacheControl(string expected) => Assert.Contains(expected, testBase.LastResponse!.Headers.CacheControl!.ToString());

    [Then("回應的 WWW-Authenticate 應包含錯誤碼 \"(.*)\"")]
    public void ThenWwwAuthenticateError(string error) =>
        Assert.Contains($"error=\"{error}\"", testBase.LastResponse!.Headers.WwwAuthenticate.ToString());

    [Then("回應應包含 WWW-Authenticate 標頭")]
    public void ThenWwwAuthenticate() => Assert.Contains("Bearer", testBase.LastResponse!.Headers.WwwAuthenticate.Select(header => header.Scheme));

    private async Task CallAsync(string? accessToken, HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, "/connect/userinfo");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        // RFC 6750 §2.2：POST 需為 application/x-www-form-urlencoded；Token 仍以 Authorization 標頭傳遞。
        if (request.Method == HttpMethod.Post)
        {
            request.Content = new FormUrlEncodedContent([]);
        }

        testBase.LastResponse = await testBase.Client.SendAsync(request);
        if (testBase.LastResponse.IsSuccessStatusCode)
        {
            this._claims = await testBase.LastResponse.Content.ReadFromJsonAsync<JsonElement>();
        }
    }
}
