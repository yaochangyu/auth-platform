using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DeveloperApi.Tests.Support;
using Microsoft.IdentityModel.Tokens;
using Reqnroll;

namespace DeveloperApi.Tests.Steps;

[Binding]
public class ApplicationSteps(DeveloperApiTestBase testBase)
{
    private readonly Dictionary<string, Guid> _applicationIds = [];
    private JsonElement _body;

    [When("開發者 \"([^\"]*)\" 建立應用專案，名稱 \"([^\"]*)\"、簡介 \"([^\"]*)\"、聯絡 Email \"([^\"]*)\"")]
    public Task When建立專案(string developer, string name, string description, string email) =>
        this.CreateAsync(developer, name, description, email, null, null);

    [When("開發者 \"([^\"]*)\" 建立應用專案，名稱 \"([^\"]*)\"、簡介 \"([^\"]*)\"、聯絡 Email \"([^\"]*)\"，Logo \"([^\"]*)\"、官網 \"([^\"]*)\"")]
    public Task When建立專案含選填(string developer, string name, string description, string email, string logo, string homepage) =>
        this.CreateAsync(developer, name, description, email, logo, homepage);

    [Given("開發者 \"([^\"]*)\" 已建立專案 \"([^\"]*)\"")]
    public async Task Given已建立專案(string developer, string name)
    {
        await this.CreateAsync(developer, name, "測試用簡介", "dev@example.com", null, null);
        Assert.Equal(201, (int)testBase.LastResponse!.StatusCode);
        this._applicationIds[name] = this._body.GetProperty("id").GetGuid();
    }

    [When("開發者 \"([^\"]*)\" 檢視專案 \"([^\"]*)\"")]
    public Task When檢視專案(string developer, string name) =>
        this.SendAsync(HttpMethod.Get, $"/api/v1/applications/{this._applicationIds[name]}", testBase.TokenFor(developer));

    [When("開發者 \"([^\"]*)\" 將專案 \"([^\"]*)\" 更新為名稱 \"([^\"]*)\"")]
    public Task When更新專案(string developer, string project, string newName) =>
        this.SendAsync(
            HttpMethod.Put,
            $"/api/v1/applications/{this._applicationIds[project]}",
            testBase.TokenFor(developer),
            Request(newName, "更新後的簡介", "dev@example.com", null, null));

    [When("以 (.*) 取得專案清單")]
    public Task When以指定Token取得清單(string tokenCase)
    {
        var member = Guid.NewGuid();
        var token = tokenCase switch
        {
            "未帶 Access Token" => null,
            "偽造的 Access Token" => "forged.access.token",
            "已過期的 Access Token" => TestJwtIssuer.Create(member, lifetime: TimeSpan.FromMinutes(-10)),
            "使用未知金鑰簽署的 Access Token" => TestJwtIssuer.Create(member, signingKey: new RsaSecurityKey(System.Security.Cryptography.RSA.Create(2048)) { KeyId = "test-key" }),
            "sub 不是有效會員識別碼的 Access Token" => TestJwtIssuer.CreateWithSubject("not-a-member-id"),
            "缺少 developer_api 範疇的 Access Token" => TestJwtIssuer.Create(member, scope: "openid profile email"),
            _ => throw new ArgumentOutOfRangeException(nameof(tokenCase), tokenCase, "未知的 Token 情況"),
        };
        return this.SendAsync(HttpMethod.Get, "/api/v1/applications", token);
    }

    [Then("回應的專案名稱應為 \"([^\"]*)\"")]
    public void Then專案名稱(string expected) => Assert.Equal(expected, this._body.GetProperty("name").GetString());

    [Then("回應的專案應有系統產生的 clientId")]
    public void ThenClientId() => Assert.False(string.IsNullOrWhiteSpace(this._body.GetProperty("clientId").GetString()));

    [Then("回應的專案狀態應為 \"([^\"]*)\"")]
    public void Then專案狀態(string expected) => Assert.Equal(expected, this._body.GetProperty("status").GetString());

    [Then("回應的專案 Logo 應為 \"([^\"]*)\"")]
    public void Then專案Logo(string expected) => Assert.Equal(expected, this._body.GetProperty("logoUrl").GetString());

    [Then("回應的專案官網應為 \"([^\"]*)\"")]
    public void Then專案官網(string expected) => Assert.Equal(expected, this._body.GetProperty("homepageUrl").GetString());

    [Then("回應的錯誤應指出欄位 \"([^\"]*)\"")]
    public void Then錯誤欄位(string field)
    {
        Assert.True(this._body.GetProperty("errors").TryGetProperty(field, out var messages), $"errors 應包含欄位 {field}");

        // 訊息會直接顯示在畫面上，不可退回 FluentValidation 的英文預設訊息。
        Assert.DoesNotContain("The specified condition was not met", messages[0].GetString());
    }

    [Then("開發者 \"([^\"]*)\" 的專案清單應為 \"([^\"]*)\"")]
    public async Task Then專案清單(string developer, string expectedNames)
    {
        await this.SendAsync(HttpMethod.Get, "/api/v1/applications", testBase.TokenFor(developer));
        var names = this._body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString());
        Assert.Equal(expectedNames.Length == 0 ? [] : expectedNames.Split('、'), names);
    }

    [Then("開發者 \"([^\"]*)\" 檢視專案 \"([^\"]*)\" 的名稱應為 \"([^\"]*)\"")]
    public async Task Then專案名稱為(string developer, string project, string expectedName)
    {
        await this.When檢視專案(developer, project);
        Assert.Equal(expectedName, this._body.GetProperty("name").GetString());
    }

    private static object Request(string name, string description, string email, string? logo, string? homepage) => new
    {
        name,
        description,
        contactEmail = email,
        logoUrl = string.IsNullOrEmpty(logo) ? null : logo,
        homepageUrl = string.IsNullOrEmpty(homepage) ? null : homepage,
    };

    private Task CreateAsync(string developer, string name, string description, string email, string? logo, string? homepage) =>
        this.SendAsync(HttpMethod.Post, "/api/v1/applications", testBase.TokenFor(developer), Request(name, description, email, logo, homepage));

    private async Task SendAsync(HttpMethod method, string url, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        testBase.LastResponse = await testBase.Client.SendAsync(request);
        var text = await testBase.LastResponse.Content.ReadAsStringAsync();
        this._body = text.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(text);
    }
}
