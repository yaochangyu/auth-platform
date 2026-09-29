using AuthServer.Tests.Support;
using AuthShared.ClientSecrets;
using Reqnroll;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Tests.Steps;

// 以與 developer-api 相同的 ClientSecretSet 操作（Issue / Revoke）建立 Secret 集合狀態，驗證 auth-server 的換票行為。
[Binding]
public class ClientSecretSteps(AuthServerTestBase testBase, TokenSteps token)
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromHours(24);

    private ClientSecretSet _set = ClientSecretSet.Empty;
    private string _firstSecret = string.Empty;
    private string _secondSecret = string.Empty;
    private Guid _firstSecretId;

    [Given("Confidential Client \"(.*)\" 已發行第一組 Secret")]
    public async Task Given已發行第一組(string clientId)
    {
        var (set, plaintext, entry) = ClientSecretSet.Empty.Issue(this.Now, GracePeriod);
        this._set = set;
        this._firstSecret = plaintext;
        this._firstSecretId = entry.Id;
        await this.SaveClientAsync(clientId);
    }

    [Given("Confidential Client \"(.*)\" 尚未發行任何 Secret")]
    public Task Given尚未發行(string clientId) => this.SaveClientAsync(clientId);

    [When("以 Client \"(.*)\" 使用佔位 Secret 兌換 Token")]
    public Task When使用佔位Secret(string clientId) => token.ExchangeWithSecretAsync(clientId, TestClientSeeder.PlaceholderSecret);

    [Given("已輪替發行第二組 Secret，第一組 Secret 進入 24 小時過渡期")]
    public async Task Given輪替發行第二組()
    {
        var (set, plaintext, _) = this._set.Issue(this.Now, GracePeriod);
        this._set = set;
        this._secondSecret = plaintext;
        await this.SaveClientAsync("rotation-test-app");
    }

    [Given("第一組 Secret 已被手動作廢")]
    public async Task Given作廢第一組()
    {
        this._set = this._set.Revoke(this._firstSecretId, this.Now)!;
        await this.SaveClientAsync("rotation-test-app");
    }

    [Given("時間已經過了 (\\d+) 小時")]
    public void Given時間已經過了(int hours) => testBase.Factory.TimeProvider.Advance(TimeSpan.FromHours(hours));

    [When("以 Client \"(.*)\" 使用第一組 Secret 兌換 Token")]
    public Task When使用第一組(string clientId) => token.ExchangeWithSecretAsync(clientId, this._firstSecret);

    [When("以 Client \"(.*)\" 使用第二組 Secret 兌換 Token")]
    public Task When使用第二組(string clientId) => token.ExchangeWithSecretAsync(clientId, this._secondSecret);

    [When("以 Client \"(.*)\" 使用未發行過的 Secret 兌換 Token")]
    public Task When使用未發行過的Secret(string clientId) => token.ExchangeWithSecretAsync(clientId, "cs_never-issued-secret");

    private DateTimeOffset Now => testBase.Factory.TimeProvider.GetUtcNow();

    private Task SaveClientAsync(string clientId) =>
        TestClientSeeder.SaveConfidentialAsync(
            testBase.Factory.Services, clientId, this._set, clientCredentials: true, Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess);
}
