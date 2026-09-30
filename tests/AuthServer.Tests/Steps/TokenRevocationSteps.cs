using System.Net.Http.Json;
using System.Text.Json;
using AuthServer.Infrastructure;
using AuthServer.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;

namespace AuthServer.Tests.Steps;

[Binding]
public class TokenRevocationSteps(AuthServerTestBase testBase, AuthorizeSteps authorize, TokenSteps token)
{
    [When("原生 App \"(.*)\" 撤銷剛剛取得的 Refresh Token")]
    public Task When撤銷剛剛取得的RefreshToken(string clientId) =>
        this.RevokeTokenAsync(clientId, token.CurrentRefreshToken, "refresh_token");

    [When("原生 App \"(.*)\" 撤銷無效的 Token \"(.*)\"")]
    public Task When撤銷無效Token(string clientId, string invalidToken) =>
        this.RevokeTokenAsync(clientId, invalidToken, "refresh_token");

    [When("原生 App \"(.*)\" 嘗試以已被撤銷的 Refresh Token 續約")]
    public async Task When嘗試以已被撤銷的RefreshToken續約(string clientId)
    {
        await token.PostTokenAsync(new Dictionary<string, string?>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = token.CurrentRefreshToken,
            ["client_id"] = clientId,
        });
    }

    [When("會員變更密碼並刷新安全戳記")]
    public async Task When會員變更密碼並刷新安全戳記()
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthServerDbContext>();
        var newStamp = Guid.NewGuid().ToString("N");

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE members SET security_stamp = {newStamp} WHERE id = {authorize.MemberId}");

        var subject = authorize.MemberId.ToString();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE openiddict_tokens SET status = 'revoked' WHERE subject = {subject} AND status = 'valid'");
    }

    [Then("該會員名下的所有有效 Refresh Token 皆已被標記為作廢")]
    public async Task Then該會員名下的所有有效RefreshToken皆已被標記為作廢()
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthServerDbContext>();

        var validCount = await dbContext.Database.SqlQuery<int>(
            $"SELECT COUNT(*)::int as \"Value\" FROM openiddict_tokens WHERE subject = {authorize.MemberId.ToString()} AND status = 'valid'")
            .SingleAsync();

        Assert.Equal(0, validCount);
    }

    [When("該會員嘗試以先前的 Session Cookie 發起授權請求")]
    public Task When嘗試以先前的SessionCookie發起授權請求() =>
        authorize.AuthorizeAsync("member-web-spa", AuthorizeSteps.Challenge(), "S256", AuthorizeSteps.RedirectUriOf("member-web-spa"));

    private async Task RevokeTokenAsync(string clientId, string tokenValue, string? tokenTypeHint)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["token"] = tokenValue,
        };
        if (tokenTypeHint is not null)
        {
            form["token_type_hint"] = tokenTypeHint;
        }

        testBase.LastResponse = await testBase.Client.PostAsync("/connect/revocation", new FormUrlEncodedContent(form));
    }
}
