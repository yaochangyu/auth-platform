using System.Security.Cryptography;
using System.Text;
using AuthShared.ClientSecrets;
using DeveloperApi.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Reqnroll;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DeveloperApi.Tests.Steps;

[Binding]
public class OAuthClientSteps(DeveloperApiTestBase testBase)
{
    private readonly List<Guid> _secretIds = [];
    private string _lastSecret = string.Empty;

    [When("開發者 \"([^\"]*)\" 取得專案 \"([^\"]*)\" 的 OAuth 設定")]
    public Task When取得設定(string developer, string project) =>
        testBase.SendAsync(HttpMethod.Get, this.Url(project), testBase.TokenFor(developer));

    [When("開發者 \"([^\"]*)\" 設定專案 \"([^\"]*)\" 的 OAuth Client：類型 \"([^\"]*)\"、Redirect URIs \"([^\"]*)\"、Post Logout URIs \"([^\"]*)\"、範疇 \"([^\"]*)\"")]
    public Task When設定(string developer, string project, string type, string redirectUris, string postLogoutUris, string scopes) =>
        testBase.SendAsync(HttpMethod.Put, this.Url(project), testBase.TokenFor(developer), new
        {
            clientType = type,
            redirectUris = Split(redirectUris),
            postLogoutRedirectUris = Split(postLogoutUris),
            scopes = Split(scopes),
        });

    [Given("開發者 \"([^\"]*)\" 已將專案 \"([^\"]*)\" 設為 Confidential Client")]
    public async Task Given設為Confidential(string developer, string project)
    {
        await this.When設定(developer, project, "Confidential", "https://app.example.com/callback", "", "openid、profile");
        Assert.Equal(200, (int)testBase.LastResponse!.StatusCode);
    }

    [Given("開發者 \"([^\"]*)\" 已為專案 \"([^\"]*)\" 產生 Client Secret")]
    [When("開發者 \"([^\"]*)\" 為專案 \"([^\"]*)\" 產生 Client Secret")]
    public async Task When產生Secret(string developer, string project)
    {
        await testBase.SendAsync(HttpMethod.Post, this.Url(project) + "/secrets", testBase.TokenFor(developer));
        if (testBase.LastResponse!.IsSuccessStatusCode)
        {
            this._secretIds.Add(testBase.LastBody.GetProperty("id").GetGuid());
            this._lastSecret = testBase.LastBody.GetProperty("secret").GetString()!;
        }
    }

    [Given("開發者 \"([^\"]*)\" 已作廢專案 \"([^\"]*)\" 的第 (\\d+) 組 Secret")]
    [When("開發者 \"([^\"]*)\" 作廢專案 \"([^\"]*)\" 的第 (\\d+) 組 Secret")]
    public Task When作廢Secret(string developer, string project, int index) =>
        testBase.SendAsync(HttpMethod.Delete, $"{this.Url(project)}/secrets/{this._secretIds[index - 1]}", testBase.TokenFor(developer));

    [When("開發者 \"([^\"]*)\" 對專案 \"([^\"]*)\" 執行 (.*)")]
    public Task When對他人專案執行(string developer, string project, string action) => action switch
    {
        "取得 OAuth 設定" => this.When取得設定(developer, project),
        "更新 OAuth 設定" => this.When設定(developer, project, "Public", "https://app.example.com/callback", "", "openid"),
        "產生 Client Secret" => this.When產生Secret(developer, project),
        "作廢 Client Secret" => testBase.SendAsync(HttpMethod.Delete, $"{this.Url(project)}/secrets/{Guid.NewGuid()}", testBase.TokenFor(developer)),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "未知的動作"),
    };

    [Given("時間已經過了 (\\d+) 小時")]
    public void Given時間已經過了(int hours) => testBase.Factory.TimeProvider.Advance(TimeSpan.FromHours(hours));

    [Then("回應的客戶端類型應為 \"([^\"]*)\"")]
    public void Then客戶端類型(string expected) => Assert.Equal(expected, testBase.LastBody.GetProperty("clientType").GetString());

    [Then("回應的 clientId 應與專案 \"([^\"]*)\" 相同")]
    public void ThenClientId(string project) =>
        Assert.Equal(this.ClientIdOf(project), testBase.LastBody.GetProperty("clientId").GetString());

    [Then("回應的 Redirect URIs 應為 \"([^\"]*)\"")]
    public void ThenRedirectUris(string expected) => Assert.Equal(Split(expected), Strings("redirectUris"));

    [Then("回應的 Post Logout URIs 應為 \"([^\"]*)\"")]
    public void ThenPostLogoutUris(string expected) => Assert.Equal(Split(expected), Strings("postLogoutRedirectUris"));

    [Then("回應的範疇應為 \"([^\"]*)\"")]
    public void Then範疇(string expected) => Assert.Equal(Split(expected).Order(), Strings("scopes").Order());

    [Then("回應的 Secret 清單應有 (\\d+) 組")]
    public void ThenSecret數量(int expected) => Assert.Equal(expected, testBase.LastBody.GetProperty("secrets").GetArrayLength());

    [Then("回應的 Secret 明文應以 \"([^\"]*)\" 開頭")]
    public void ThenSecret明文(string prefix) => Assert.StartsWith(prefix, testBase.LastBody.GetProperty("secret").GetString());

    [Then("回應應禁止快取")]
    public void Then禁止快取() => Assert.Contains("no-store", testBase.LastResponse!.Headers.CacheControl!.ToString());

    [Then("回應內容不應含有先前發行的 Secret 明文")]
    public void Then不含明文() => Assert.DoesNotContain(this._lastSecret, testBase.LastBodyText);

    [Then("第 (\\d+) 組 Secret 的狀態應為 \"([^\"]*)\"")]
    public void ThenSecret狀態(int index, string status) =>
        Assert.Equal(status, testBase.LastBody.GetProperty("secrets")[index - 1].GetProperty("status").GetString());

    [Then("第 (\\d+) 組 Secret 的狀態應為 \"([^\"]*)\"，並於 24 小時後到期")]
    public void ThenSecret狀態與到期(int index, string status)
    {
        var secret = testBase.LastBody.GetProperty("secrets")[index - 1];
        Assert.Equal(status, secret.GetProperty("status").GetString());
        Assert.Equal(testBase.Factory.TimeProvider.GetUtcNow().AddHours(24), secret.GetProperty("expiresAt").GetDateTimeOffset());
    }

    [Then("Auth Server 中專案 \"([^\"]*)\" 的 Client 應為 Public、需要會員同意、強制 PKCE，範疇為 \"([^\"]*)\"")]
    public async Task ThenAuthServerClient(string project, string scopes)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = (await applications.FindByClientIdAsync(this.ClientIdOf(project)))!;

        Assert.Equal(ClientTypes.Public, await applications.GetClientTypeAsync(application));
        Assert.Equal(ConsentTypes.Explicit, await applications.GetConsentTypeAsync(application));
        Assert.Contains(Requirements.Features.ProofKeyForCodeExchange, await applications.GetRequirementsAsync(application));
        Assert.Equal(
            Split(scopes).Order(),
            (await applications.GetPermissionsAsync(application)).Where(p => p.StartsWith(Permissions.Prefixes.Scope)).Select(p => p[Permissions.Prefixes.Scope.Length..]).Order());
        Assert.False((await applications.GetPropertiesAsync(application)).ContainsKey(ClientSecretSet.PropertyName));
    }

    [Then("Auth Server 中專案 \"([^\"]*)\" 的 Client 是否允許 Client Credentials 應為 \"([^\"]*)\"")]
    public async Task ThenAuthServerClientCredentials(string project, string allowed)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = (await applications.FindByClientIdAsync(this.ClientIdOf(project)))!;
        Assert.Equal(allowed == "是", (await applications.GetPermissionsAsync(application)).Contains(Permissions.GrantTypes.ClientCredentials));
    }

    [Then("Auth Server 中專案 \"([^\"]*)\" 的 Client 顯示名稱應為 \"([^\"]*)\"")]
    public async Task ThenAuthServer顯示名稱(string project, string expected)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = (await applications.FindByClientIdAsync(this.ClientIdOf(project)))!;
        Assert.Equal(expected, await applications.GetDisplayNameAsync(application));
    }

    [Then("資料庫中專案 \"([^\"]*)\" 的 Secret 只存 SHA-256 雜湊而不含明文")]
    public async Task Then只存雜湊(string project)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = (await applications.FindByClientIdAsync(this.ClientIdOf(project)))!;
        var stored = (await applications.GetPropertiesAsync(application))[ClientSecretSet.PropertyName].GetRawText();

        Assert.DoesNotContain(this._lastSecret, stored);
        Assert.Contains(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(this._lastSecret))), stored);
    }

    private static string[] Split(string value) => value.Length == 0 ? [] : value.Split('、');

    private string[] Strings(string property) =>
        [.. testBase.LastBody.GetProperty(property).EnumerateArray().Select(item => item.GetString()!)];

    private string Url(string project) => $"/api/v1/applications/{testBase.ApplicationIds[project]}/oauth-client";

    private string ClientIdOf(string project) => testBase.ClientIds[project];
}
