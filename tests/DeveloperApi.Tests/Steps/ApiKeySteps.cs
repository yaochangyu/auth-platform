using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeveloperApi.Infrastructure;
using DeveloperApi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace DeveloperApi.Tests.Steps;

[Binding]
public class ApiKeySteps(DeveloperApiTestBase testBase)
{
    private const string EchoPath = "/api/v1/m2m/echo";
    private const string Body = "{\"orderId\":42}";

    private readonly List<(Guid Id, string ApiKey, string ApiSecret)> _keys = [];
    private string _lastKey = string.Empty;
    private string _lastSecret = string.Empty;

    [When("開發者 \"([^\"]*)\" 為專案 \"([^\"]*)\" 建立 API Key，名稱 \"([^\"]*)\"、環境 \"([^\"]*)\"、範疇 \"([^\"]*)\"、到期 \"([^\"]*)\"")]
    public async Task When建立(string developer, string project, string name, string environment, string scopes, string expiry)
    {
        await testBase.SendAsync(HttpMethod.Post, this.Url(project), testBase.TokenFor(developer), new
        {
            name,
            environment,
            scopes = scopes.Length == 0 ? [] : scopes.Split('、'),
            expiresAt = this.ExpiryFromNow(expiry),
        });

        if (testBase.LastResponse!.IsSuccessStatusCode)
        {
            this._lastKey = testBase.LastBody.GetProperty("apiKey").GetString()!;
            this._lastSecret = testBase.LastBody.GetProperty("apiSecret").GetString()!;
            this._keys.Add((testBase.LastBody.GetProperty("id").GetGuid(), this._lastKey, this._lastSecret));
        }
    }

    [Given("開發者 \"([^\"]*)\" 已為專案 \"([^\"]*)\" 建立 API Key，範疇 \"([^\"]*)\"，到期 \"([^\"]*)\"")]
    public async Task Given已建立(string developer, string project, string scopes, string expiry)
    {
        await this.When建立(developer, project, "整合測試", "Live", scopes, expiry);
        Assert.Equal(201, (int)testBase.LastResponse!.StatusCode);
    }

    [When("開發者 \"([^\"]*)\" 取得專案 \"([^\"]*)\" 的 API Key 清單")]
    public Task When列出(string developer, string project) =>
        testBase.SendAsync(HttpMethod.Get, this.Url(project), testBase.TokenFor(developer));

    [When("開發者 \"([^\"]*)\" 撤銷專案 \"([^\"]*)\" 的第 (\\d+) 把 API Key")]
    public Task When撤銷(string developer, string project, int index) =>
        testBase.SendAsync(HttpMethod.Delete, $"{this.Url(project)}/{this._keys[index - 1].Id}", testBase.TokenFor(developer));

    [When("開發者 \"([^\"]*)\" 嘗試對專案 \"([^\"]*)\" 的 API Key (建立金鑰|列出清單|撤銷金鑰)")]
    public Task When對他人專案執行(string developer, string project, string action) => action switch
    {
        "建立金鑰" => this.When建立(developer, project, "入侵", "Live", "profile", ""),
        "列出清單" => this.When列出(developer, project),
        _ => testBase.SendAsync(HttpMethod.Delete, $"{this.Url(project)}/{Guid.NewGuid()}", testBase.TokenFor(developer)),
    };

    [When("以第 (\\d+) 把 API Key 簽章呼叫 M2M 連線檢查端點")]
    public Task When簽章呼叫(int index) => this.SendSignedAsync(this._keys[index - 1].ApiKey, this._keys[index - 1].ApiSecret);

    [When("以第 (\\d+) 把 API Key 簽章呼叫 M2M 連線檢查端點，時間戳記為 (\\d+) 分鐘前")]
    public Task When簽章呼叫舊時間戳記(int index, int minutesAgo) =>
        this.SendSignedAsync(this._keys[index - 1].ApiKey, this._keys[index - 1].ApiSecret, timestampOffset: TimeSpan.FromMinutes(-minutesAgo));

    [When("以 (.*) 呼叫 M2M 連線檢查端點")]
    public Task When以指定情況呼叫(string scenario) => scenario switch
    {
        "簽章後被篡改 Body 的請求" => this.SendSignedAsync(this._lastKey, this._lastSecret, sendBody: "{\"orderId\":9999}"),
        "簽章後被更動查詢字串的請求" => this.SendSignedAsync(this._lastKey, this._lastSecret, signedQuery: "?amount=1", sentQuery: "?amount=1000000"),
        "時間戳記為 6 分鐘前的請求" => this.SendSignedAsync(this._lastKey, this._lastSecret, timestampOffset: TimeSpan.FromMinutes(-6)),
        "時間戳記為 6 分鐘後的請求" => this.SendSignedAsync(this._lastKey, this._lastSecret, timestampOffset: TimeSpan.FromMinutes(6)),
        "以錯誤的 API Secret 簽章的請求" => this.SendSignedAsync(this._lastKey, "as_wrong-secret"),
        "使用不存在的 API Key 的請求" => this.SendSignedAsync("ak_live_does-not-exist", this._lastSecret),
        "缺少簽章標頭的請求" => this.SendSignedAsync(this._lastKey, this._lastSecret, omitSignature: true),
        "簽章格式不是 16 進位的請求" => this.SendSignedAsync(this._lastKey, this._lastSecret, forcedSignature: "not-hex-zzzz"),
        "簽章後 Body 超過 1 MB 的請求" => this.SendSignedAsync(this._lastKey, this._lastSecret, sendBody: new string('x', 1024 * 1024 + 1), signedBody: new string('x', 1024 * 1024 + 1)),
        "時間戳記為極端數值的請求" => this.SendSignedAsync(this._lastKey, this._lastSecret, forcedTimestamp: long.MaxValue.ToString()),
        "API Secret 無法解密的 API Key 請求" => this.CorruptSecretThenSendAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "未知的情況"),
    };

    [Then("回應的 API Key 應以 \"([^\"]*)\" 開頭")]
    public void ThenApiKey前綴(string prefix) => Assert.StartsWith(prefix, testBase.LastBody.GetProperty("apiKey").GetString());

    [Then("回應的 API Secret 應以 \"([^\"]*)\" 開頭")]
    public void ThenApiSecret前綴(string prefix) => Assert.StartsWith(prefix, testBase.LastBody.GetProperty("apiSecret").GetString());

    [Then("資料庫中的 API Key 只存 SHA-256 雜湊而不含 API Key 與 API Secret 明文")]
    public async Task Then只存雜湊()
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DeveloperApiDbContext>();
        var id = this._keys[^1].Id;
        var row = await dbContext.ApiKeys.AsNoTracking().SingleAsync(key => key.Id == id);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(this._lastKey))), row.KeyHash);

        // 整列資料序列化後都不得出現明文（API Secret 以 Data Protection 加密保存）。
        var stored = JsonSerializer.Serialize(row);
        Assert.DoesNotContain(this._lastKey, stored);
        Assert.DoesNotContain(this._lastSecret, stored);
    }

    [Then("API Key 清單應有 (\\d+) 把，且只顯示前綴而不含金鑰與密鑰明文")]
    public void Then清單(int count)
    {
        Assert.Equal(count, testBase.LastBody.GetProperty("items").GetArrayLength());
        Assert.Equal(this._lastKey[..16], testBase.LastBody.GetProperty("items")[0].GetProperty("prefix").GetString());
        Assert.DoesNotContain(this._lastKey, testBase.LastBodyText);
        Assert.DoesNotContain(this._lastSecret, testBase.LastBodyText);
    }

    [Then("M2M 回應的 clientId 應為專案 \"([^\"]*)\" 的 ClientId")]
    public void ThenM2mClientId(string project) =>
        Assert.Equal(testBase.ClientIds[project], testBase.LastBody.GetProperty("clientId").GetString());

    [Then("M2M 回應的範疇應為 \"([^\"]*)\"")]
    public void ThenM2m範疇(string scopes) =>
        Assert.Equal(scopes.Split('、').Order(), testBase.LastBody.GetProperty("scopes").EnumerateArray().Select(item => item.GetString()!).Order());

    private string Url(string project) => $"/api/v1/applications/{testBase.ApplicationIds[project]}/api-keys";

    private DateTimeOffset? ExpiryFromNow(string expiry)
    {
        if (expiry.Length == 0)
        {
            return null;
        }

        return testBase.Factory.TimeProvider.GetUtcNow().AddDays(int.Parse(expiry.Split(' ')[0]));
    }

    // 獨立於 AuthShared 之外，依 OpenAPI 文件描述的格式自行計算簽章：
    // METHOD \n PATH_AND_QUERY \n TIMESTAMP \n SHA256_HEX(BODY)，再以 API Secret 做 HMAC-SHA256（小寫 16 進位）。
    private static string Sign(string secret, string method, string pathAndQuery, long timestamp, string body)
    {
        var canonical = $"{method}\n{pathAndQuery}\n{timestamp}\n{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant()}";
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    // 模擬 Data Protection 金鑰環遺失：把密文改成無法解密的內容。
    private async Task CorruptSecretThenSendAsync()
    {
        await using (var scope = testBase.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DeveloperApiDbContext>();
            var id = this._keys[^1].Id;
            await dbContext.ApiKeys.Where(key => key.Id == id).ExecuteUpdateAsync(setters => setters.SetProperty(key => key.SecretProtected, "not-a-valid-ciphertext"));
        }

        await this.SendSignedAsync(this._lastKey, this._lastSecret);
    }

    private async Task SendSignedAsync(
        string apiKey,
        string secret,
        TimeSpan? timestampOffset = null,
        string? sendBody = null,
        string signedQuery = "",
        string? sentQuery = null,
        bool omitSignature = false,
        string? forcedSignature = null,
        string? signedBody = null,
        string? forcedTimestamp = null)
    {
        var timestamp = testBase.Factory.TimeProvider.GetUtcNow().Add(timestampOffset ?? TimeSpan.Zero).ToUnixTimeSeconds();
        using var request = new HttpRequestMessage(HttpMethod.Post, EchoPath + (sentQuery ?? signedQuery))
        {
            Content = new StringContent(sendBody ?? Body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Api-Key", apiKey);
        request.Headers.Add("X-Timestamp", forcedTimestamp ?? timestamp.ToString());
        if (!omitSignature)
        {
            request.Headers.Add("X-Signature", forcedSignature ?? Sign(secret, "POST", EchoPath + signedQuery, timestamp, signedBody ?? Body));
        }

        testBase.LastResponse = await testBase.Client.SendAsync(request);
        var text = await testBase.LastResponse.Content.ReadAsStringAsync();
        testBase.SetLastBody(text);
    }
}
