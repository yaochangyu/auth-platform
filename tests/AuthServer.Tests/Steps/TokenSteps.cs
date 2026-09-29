using System.Net.Http.Json;
using System.Text.Json;
using AuthServer.Tests.Support;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Reqnroll;

namespace AuthServer.Tests.Steps;

[Binding]
public class TokenSteps(AuthServerTestBase testBase, AuthorizeSteps authorize)
{
    private string _code = string.Empty;
    private JsonElement _token;
    private string _refreshToken = string.Empty;
    private string _rotatedRefreshToken = string.Empty;

    [Given("已以 Client \"(.*)\" 取得 Authorization Code")]
    public async Task Given取得Code(string clientId)
    {
        // 要求 offline_access 才會核發 Refresh Token（OpenIddict / OIDC 規範）。
        await authorize.AuthorizeAsync(
            clientId, AuthorizeSteps.Challenge(), "S256", AuthorizeSteps.RedirectUriOf(clientId), scope: "openid profile email offline_access");
        this._code = QueryHelpers.ParseQuery(authorize.Location!.Query)["code"]!;
        Assert.False(string.IsNullOrEmpty(this._code));
    }

    public bool ResponseHas(string property) => this._token.TryGetProperty(property, out _);

    public string InitialAccessToken { get; private set; } = string.Empty;

    public string AccessToken => this._token.GetProperty("access_token").GetString()!;

    [Given("已以 Client \"(.*)\" 取得僅含範疇 \"(.*)\" 的 Access Token")]
    public async Task Given取得指定範疇的AccessToken(string clientId, string scopes)
    {
        await authorize.AuthorizeAsync(
            clientId, AuthorizeSteps.Challenge(), "S256", AuthorizeSteps.RedirectUriOf(clientId), scope: scopes.Replace('、', ' '));
        this._code = QueryHelpers.ParseQuery(authorize.Location!.Query)["code"]!;
        await this.ExchangeAsync(clientId);
        Assert.Equal(200, (int)testBase.LastResponse!.StatusCode);
    }

    [Given("已以 Client \"(.*)\" 取得不含 offline_access 的 Authorization Code")]
    public async Task Given取得不含OfflineAccess的Code(string clientId)
    {
        await authorize.AuthorizeAsync(
            clientId, AuthorizeSteps.Challenge(), "S256", AuthorizeSteps.RedirectUriOf(clientId), scope: "openid profile email");
        this._code = QueryHelpers.ParseQuery(authorize.Location!.Query)["code"]!;
    }

    [Given("已以 Client \"(.*)\" 兌換取得 Token")]
    public async Task Given兌換取得Token(string clientId)
    {
        await this.Given取得Code(clientId);
        await this.ExchangeAsync(clientId);
        Assert.Equal(200, (int)testBase.LastResponse!.StatusCode);
        this.InitialAccessToken = this.AccessToken;
    }

    [Given("以 Client \"(.*)\" 及正確的 code_verifier 兌換 Token")]
    [When("以 Client \"(.*)\" 及正確的 code_verifier 兌換 Token")]
    public Task When正確兌換(string clientId) => this.ExchangeAsync(clientId);

    [When("以 Client \"(.*)\" 及正確的 code_verifier 與 client_secret 兌換 Token")]
    public Task When含Secret兌換(string clientId) => this.ExchangeAsync(clientId, secret: TestSecret);

    [When("以 Client \"(.*)\" 兌換 Token 且 提供錯誤的 client_secret")]
    public Task When錯誤Secret(string clientId) => this.ExchangeAsync(clientId, secret: "wrong-secret");

    [When("以 Client \"(.*)\" 兌換 Token 且 未提供 client_secret")]
    public Task When缺少Secret(string clientId) => this.ExchangeAsync(clientId);

    [When("以 Client \"(.*)\" 兌換 Token 且 code_verifier 不符")]
    public Task WhenVerifier不符(string clientId) => this.ExchangeAsync(clientId, verifier: "another-code-verifier-that-does-not-match-1234567");

    [When("以 Client \"(.*)\" 兌換 Token 且 未提供 code_verifier")]
    public Task When缺少Verifier(string clientId) => this.ExchangeAsync(clientId, verifier: null);

    [When("以 Client \"(.*)\" 兌換 Token 且 redirect_uri 與授權請求不一致")]
    public Task WhenRedirectUri不一致(string clientId) => this.ExchangeAsync(clientId, redirectUri: "https://member.1111.com.tw/other");

    [Given("已以 Refresh Token 續約")]
    [When("以 Refresh Token 續約")]
    public async Task When續約()
    {
        this._rotatedRefreshToken = this._refreshToken;
        await this.RefreshAsync(this._refreshToken);
    }

    [Given("重複使用已被輪替的舊 Refresh Token 續約")]
    [When("重複使用已被輪替的舊 Refresh Token 續約")]
    public Task When重複使用舊Token() => this.RefreshAsync(this._rotatedRefreshToken);

    [When("使用輪替後取得的新 Refresh Token 續約")]
    public Task When使用新Token() => this.RefreshAsync(this._refreshToken);

    [Then("回應的 token_type 應為 \"(.*)\"")]
    public void ThenTokenType(string expected) => Assert.Equal(expected, this._token.GetProperty("token_type").GetString());

    [Then("Access Token 應為 RS256 簽章且可用 JWKS 公開金鑰驗證")]
    public async Task ThenAccessToken簽章()
    {
        var accessToken = this._token.GetProperty("access_token").GetString()!;
        Assert.Equal("RS256", new JsonWebToken(accessToken).Alg);

        var jwks = await testBase.Client.GetStringAsync("/.well-known/jwks.json");
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken, new TokenValidationParameters
        {
            IssuerSigningKeys = new JsonWebKeySet(jwks).GetSigningKeys(),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
        });
        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Then("Access Token 的 sub 應為該會員")]
    public void ThenAccessTokenSub() =>
        Assert.Equal(authorize.MemberId.ToString(), new JsonWebToken(this._token.GetProperty("access_token").GetString()).Subject);

    [Then("Access Token 的有效期間應為 (\\d+) 分鐘")]
    public void ThenAccessToken有效期間(int minutes)
    {
        var token = new JsonWebToken(this._token.GetProperty("access_token").GetString());
        Assert.Equal(TimeSpan.FromMinutes(minutes), token.ValidTo - token.IssuedAt);
    }

    [Then("回應應包含 aud 為 \"(.*)\" 的 ID Token")]
    public void ThenIdToken(string audience)
    {
        var idToken = new JsonWebToken(this._token.GetProperty("id_token").GetString());
        Assert.Contains(audience, idToken.Audiences);
        Assert.Equal(authorize.MemberId.ToString(), idToken.Subject);
    }

    [Then("Access Token 不應包含 security_stamp")]
    public void ThenAccessToken無Stamp() =>
        Assert.False(new JsonWebToken(this._token.GetProperty("access_token").GetString()).TryGetPayloadValue<string>("security_stamp", out _));

    [Then("回應不應包含 Refresh Token")]
    public void Then無RefreshToken() => Assert.False(this._token.TryGetProperty("refresh_token", out _));

    [Then("Access Token 的 scope 應包含 \"(.*)\"")]
    public void ThenAccessToken範疇(string scope)
    {
        var token = new JsonWebToken(this._token.GetProperty("access_token").GetString());
        Assert.Contains(scope, token.Claims.Where(claim => claim.Type == "scope").SelectMany(claim => claim.Value.Split(' ')));
    }

    [Then("回應應包含 Refresh Token")]
    public void ThenRefreshToken() => Assert.False(string.IsNullOrEmpty(this._token.GetProperty("refresh_token").GetString()));

    [Then("回應應包含新的 Access Token")]
    public void Then新的AccessToken() => Assert.False(string.IsNullOrEmpty(this._token.GetProperty("access_token").GetString()));

    [Then("回應的新 Refresh Token 應與舊的不同")]
    public void Then新的RefreshToken不同() => Assert.NotEqual(this._rotatedRefreshToken, this._refreshToken);

    [Then("回應的 error 欄位應為 \"(.*)\"")]
    public void ThenError(string expected) => Assert.Equal(expected, this._token.GetProperty("error").GetString());

    [Then("回應不應包含 access_token")]
    public void Then無AccessToken() => Assert.False(this._token.TryGetProperty("access_token", out _));

    // 測試專用種子密鑰，與 AuthServerWebApplicationFactory 的 Auth:DemoClientSecret 一致。
    public const string TestSecret = "demo-secret-for-tests";

    public Task ExchangeWithSecretAsync(string clientId, string secret) => this.ExchangeAsync(clientId, secret: secret);

    private Task ExchangeAsync(string clientId, string? verifier = AuthorizeSteps.Verifier, string? secret = null, string? redirectUri = null) =>
        this.PostTokenAsync(new Dictionary<string, string?>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = this._code,
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri ?? AuthorizeSteps.RedirectUriOf(clientId),
            ["code_verifier"] = verifier,
            ["client_secret"] = secret,
        });

    private Task RefreshAsync(string refreshToken) =>
        this.PostTokenAsync(new Dictionary<string, string?>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = "member-web-spa",
        });

    public async Task PostTokenAsync(Dictionary<string, string?> form)
    {
        var content = new FormUrlEncodedContent(form.Where(pair => pair.Value is not null).Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value!)));
        testBase.LastResponse = await testBase.Client.PostAsync("/connect/token", content);
        this._token = await testBase.LastResponse.Content.ReadFromJsonAsync<JsonElement>();

        if (testBase.LastResponse.IsSuccessStatusCode && this._token.TryGetProperty("refresh_token", out var refresh))
        {
            this._refreshToken = refresh.GetString()!;
        }
    }
}
