using System.Net.Http.Json;
using System.Text.Json;
using AuthServer.Tests.Support;
using Reqnroll;

namespace AuthServer.Tests.Steps;

[Binding]
public class DiscoverySteps(AuthServerTestBase testBase)
{
    private JsonElement _body;
    private string _keysBeforeRestart = string.Empty;

    [Given("初始化 Auth Server 測試伺服器")]
    public void Given初始化AuthServer測試伺服器() => testBase.Start();

    [AfterScenario]
    public async Task 釋放測試伺服器()
    {
        await testBase.StopAsync();
        testBase.DeleteKeyDirectory();
    }

    [When("使用者呼叫 GET \\/.well-known\\/openid-configuration 端點")]
    public Task When呼叫OpenIdConfiguration() => this.GetAsync("/.well-known/openid-configuration");

    [When("使用者呼叫 GET \\/.well-known\\/jwks.json 端點")]
    [When("使用者再次呼叫 GET \\/.well-known\\/jwks.json 端點")]
    public Task When呼叫Jwks() => this.GetAsync("/.well-known/jwks.json");

    [Given("Auth Server 已啟動並取得目前的 JWKS 公開金鑰")]
    public async Task GivenAuthServer已啟動並取得目前的Jwks()
    {
        await this.GetAsync("/.well-known/jwks.json");
        this._keysBeforeRestart = this._body.GetProperty("keys").GetRawText();
    }

    [When("重新啟動 Auth Server 測試伺服器")]
    public async Task When重新啟動AuthServer測試伺服器()
    {
        await testBase.StopAsync();
        testBase.Start();
    }

    [Then("回應狀態碼應為 (\\d+)")]
    public void Then回應狀態碼應為(int expected) => Assert.Equal(expected, (int)testBase.LastResponse!.StatusCode);

    [Then("回應內容的 issuer 欄位應與伺服器位址一致")]
    public void Then回應內容的Issuer應與伺服器位址一致()
    {
        var issuer = this._body.GetProperty("issuer").GetString()!;
        Assert.Equal(testBase.Client.BaseAddress!.ToString().TrimEnd('/'), issuer.TrimEnd('/'));
    }

    [Then("回應內容的 jwks_uri 欄位應為 \"(.*)\" 的完整網址")]
    public void Then回應內容的JwksUri應為完整網址(string path)
    {
        var expected = new Uri(testBase.Client.BaseAddress!, path).ToString();
        Assert.Equal(expected, this._body.GetProperty("jwks_uri").GetString());
    }

    [Then("回應內容的 keys 陣列應至少包含一組公開金鑰")]
    public void Then回應內容的Keys應至少包含一組公開金鑰() =>
        Assert.True(this._body.GetProperty("keys").GetArrayLength() >= 1);

    [Then("該公開金鑰的 kty 欄位應為 \"(.*)\"")]
    public void Then公開金鑰的Kty應為(string expected)
    {
        var key = this._body.GetProperty("keys")[0];
        Assert.Equal(expected, key.GetProperty("kty").GetString());
        Assert.False(key.TryGetProperty("d", out _), "JWKS 不可包含私鑰欄位");
    }

    [Then("回應內容的公開金鑰應與重啟前完全相同")]
    public void Then公開金鑰應與重啟前完全相同() =>
        Assert.Equal(this._keysBeforeRestart, this._body.GetProperty("keys").GetRawText());

    private async Task GetAsync(string path)
    {
        testBase.LastResponse = await testBase.Client.GetAsync(path);
        this._body = await testBase.LastResponse.Content.ReadFromJsonAsync<JsonElement>();
    }
}
