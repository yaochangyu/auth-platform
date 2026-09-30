using System.Net.Http.Json;
using System.Text.Json;
using AuthServer.Tests.Support;
using Microsoft.AspNetCore.WebUtilities;
using Reqnroll;

namespace AuthServer.Tests.Steps;

[Binding]
public class PublicClientPkceSteps(AuthServerTestBase testBase, AuthorizeSteps authorize, TokenSteps token)
{
    private string _clientId = string.Empty;
    private string _redirectUri = string.Empty;
    private string _code = string.Empty;

    [Given("已註冊原生 App 公用客戶端 \"(.*)\"，其 Universal Link 為 \"(.*)\"")]
    public async Task Given已註冊原生App公用客戶端(string clientId, string redirectUri)
    {
        this._clientId = clientId;
        this._redirectUri = redirectUri;
        AuthorizeSteps.RedirectUris[clientId] = redirectUri;
        await TestClientSeeder.SavePublicAppAsync(
            testBase.Factory.Services, clientId, redirectUri, "openid", "profile", "email", "offline_access");
    }

    [When("原生 App \"(.*)\" 以合法的 S256 PKCE 與重定向網址發起授權請求")]
    public async Task When原生App以合法的S256Pkce發起授權請求(string clientId)
    {
        this._clientId = clientId;
        await authorize.AuthorizeAsync(
            clientId,
            AuthorizeSteps.Challenge(),
            "S256",
            this._redirectUri,
            scope: "openid profile email offline_access");
    }

    [When("原生 App \"(.*)\" 以重定向網址 \"(.*)\" 發起授權請求")]
    public async Task When原生App以指定網址發起授權請求(string clientId, string redirectUri)
    {
        this._clientId = clientId;
        await authorize.AuthorizeAsync(
            clientId,
            AuthorizeSteps.Challenge(),
            "S256",
            redirectUri,
            scope: "openid profile email offline_access");
    }

    [When("原生 App \"(.*)\" 發起授權請求且 未提供 code_challenge")]
    public async Task When未提供Challenge(string clientId)
    {
        this._clientId = clientId;
        await authorize.AuthorizeAsync(clientId, null, null, this._redirectUri);
    }

    [When("原生 App \"(.*)\" 發起授權請求且 code_challenge_method 為 plain")]
    public async Task WhenPlain(string clientId)
    {
        this._clientId = clientId;
        await authorize.AuthorizeAsync(clientId, AuthorizeSteps.Challenge(), "plain", this._redirectUri);
    }

    [When("原生 App \"(.*)\" 發起授權請求且 未提供 code_challenge_method")]
    public async Task When未提供Method(string clientId)
    {
        this._clientId = clientId;
        await authorize.AuthorizeAsync(clientId, AuthorizeSteps.Challenge(), null, this._redirectUri);
    }

    [Given("原生 App \"(.*)\" 已取得授權碼")]
    public async Task Given原生App已取得授權碼(string clientId)
    {
        this._clientId = clientId;
        await authorize.AuthorizeAsync(
            clientId,
            AuthorizeSteps.Challenge(),
            "S256",
            this._redirectUri,
            scope: "openid profile email offline_access");
        this._code = QueryHelpers.ParseQuery(authorize.Location!.Query)["code"]!;
        Assert.False(string.IsNullOrEmpty(this._code));
    }

    [Then("重定向目標應為 \"(.*)\"")]
    public void Then重定向目標(string expectedUri) =>
        Assert.Equal(expectedUri, authorize.Location!.GetLeftPart(UriPartial.Path));

    [Then("重定向網址應帶有 Authorization Code")]
    public void Then帶有Code()
    {
        var query = QueryHelpers.ParseQuery(authorize.Location!.Query);
        this._code = query["code"]!;
        Assert.False(string.IsNullOrEmpty(this._code));
    }

    [When("原生 App \"(.*)\" 僅以 code_verifier 換票且不帶 client_secret")]
    public async Task When以Verifier換票(string clientId)
    {
        await token.PostTokenAsync(new Dictionary<string, string?>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = this._code,
            ["client_id"] = clientId,
            ["redirect_uri"] = this._redirectUri,
            ["code_verifier"] = AuthorizeSteps.Verifier,
        });
    }

    [When("原生 App \"(.*)\" 以錯誤的 code_verifier 換票且不帶 client_secret")]
    public async Task When以錯誤Verifier換票(string clientId)
    {
        await token.PostTokenAsync(new Dictionary<string, string?>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = this._code,
            ["client_id"] = clientId,
            ["redirect_uri"] = this._redirectUri,
            ["code_verifier"] = "invalid-code-verifier-wrong-content",
        });
    }
}
