using AuthServer.Tests.Support;
using AuthShared.ClientSecrets;
using Microsoft.IdentityModel.JsonWebTokens;
using Reqnroll;

namespace AuthServer.Tests.Steps;

[Binding]
public class ClientCredentialsSteps(AuthServerTestBase testBase, TokenSteps token)
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromHours(24);

    private string _secret = string.Empty;

    [Given("Confidential Client \"(.*)\" 已發行 Secret，允許 Client Credentials，範疇為 \"(.*)\"")]
    public Task Given允許(string clientId, string scopes) => this.SeedAsync(clientId, scopes, clientCredentials: true);

    [Given("Confidential Client \"(.*)\" 已發行 Secret，允許 Client Credentials，範疇為 \"(.*)\"，且已被管理員停用")]
    public Task Given已停用(string clientId, string scopes) => this.SeedAsync(clientId, scopes, clientCredentials: true, suspended: true);

    [Given("Confidential Client \"(.*)\" 已發行 Secret，不允許 Client Credentials，範疇為 \"(.*)\"")]
    public Task Given不允許(string clientId, string scopes) => this.SeedAsync(clientId, scopes, clientCredentials: false);

    [Given("Public Client \"(.*)\" 已建立")]
    public Task GivenPublic(string clientId) => TestClientSeeder.SavePublicAsync(testBase.Factory.Services, clientId);

    [When("以 Client \"(.*)\" 使用 client_credentials 換票，要求範疇 \"(.*)\"")]
    public Task When換票(string clientId, string scopes) => this.PostAsync(clientId, this._secret, scopes);

    [When("以 Client \"(.*)\" 使用錯誤的 client_secret 以 client_credentials 換票")]
    public Task When錯誤Secret(string clientId) => this.PostAsync(clientId, "cs_wrong-secret", "profile");

    [When("以 Public Client \"(.*)\" 使用 client_credentials 換票")]
    public Task WhenPublic(string clientId) => this.PostAsync(clientId, null, "profile");

    [Given("Client \"(.*)\" 此後被管理員停用")]
    public Task Given此後停用(string clientId) => TestClientSeeder.SuspendAsync(testBase.Factory.Services, clientId);

    [When("以 Client \"(.*)\" 使用其 Secret 及正確的 code_verifier 兌換 Token")]
    public Task When用Secret兌換(string clientId) => token.ExchangeWithSecretAsync(clientId, this._secret);

    [Then("Access Token 的 sub 應為 Client \"(.*)\"")]
    public void ThenSub(string clientId) => Assert.Equal(clientId, new JsonWebToken(token.AccessToken).Subject);

    [Then("回應不應包含 ID Token")]
    public void Then無IdToken() => Assert.False(token.ResponseHas("id_token"));

    private DateTimeOffset Now => testBase.Factory.TimeProvider.GetUtcNow();

    private async Task SeedAsync(string clientId, string scopes, bool clientCredentials, bool suspended = false)
    {
        var (set, plaintext, _) = ClientSecretSet.Empty.Issue(this.Now, GracePeriod);
        this._secret = plaintext;
        await TestClientSeeder.SaveConfidentialAsync(testBase.Factory.Services, clientId, set, clientCredentials, suspended, scopes.Split('、'));
    }

    private Task PostAsync(string clientId, string? secret, string scopes) =>
        token.PostTokenAsync(new Dictionary<string, string?>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["scope"] = scopes.Replace('、', ' '),
        });
}
