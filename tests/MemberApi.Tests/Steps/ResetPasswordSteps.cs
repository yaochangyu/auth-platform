using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using MemberApi.Security;
using MemberApi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace MemberApi.Tests.Steps;

[Binding]
public class ResetPasswordSteps(PostgreSqlTestBase testBase, ScenarioContext scenarioContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // 必須與 AuthController 的 ForgotPasswordAcceptedMessage 常數保持完全一致，
    // 用來斷言「已註冊/未註冊 Email 的回應內容完全相同」這個防帳號枚舉的核心規範。
    private const string ExpectedForgotPasswordMessage =
        "若該信箱已在平台註冊，系統將寄出重設密碼說明信件，請於 15 分鐘內完成重設。";

    private HttpResponseMessage _response = null!;
    private ForgotPasswordResponse? _forgotPasswordResponse;

    [When("使用者以 Email \"([^\"]*)\" 呼叫忘記密碼 API")]
    public async Task When使用者以Email呼叫忘記密碼Api(string email)
    {
        var request = new ForgotPasswordRequest(email);
        this._response = await testBase.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", request);
        testBase.LastResponse = this._response;

        if (this._response.StatusCode == HttpStatusCode.OK)
        {
            this._forgotPasswordResponse = await this._response.Content.ReadFromJsonAsync<ForgotPasswordResponse>(JsonOptions);
        }
        else
        {
            testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(JsonOptions);
        }
    }

    [Then("系統應為該會員寫入一筆尚未使用的重設密碼驗證權杖")]
    public async Task Then系統應為該會員寫入一筆尚未使用的重設密碼驗證權杖()
    {
        var email = scenarioContext.Get<string>("email");
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await dbContext.Members.SingleAsync(m => m.Email == email);
        var exists = await dbContext.VerificationTokens.AnyAsync(token =>
            token.MemberId == member.Id && token.Purpose == VerificationTokenPurpose.PasswordReset && token.UsedAt == null);
        Assert.True(exists);
    }

    [Then("系統應為該會員寫入一筆待發送之重設密碼 Outbox 訊息")]
    public async Task Then系統應為該會員寫入一筆待發送之重設密碼Outbox訊息()
    {
        var email = scenarioContext.Get<string>("email");
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var exists = await dbContext.OutboxMessages.AnyAsync(message => message.ToEmail == email);
        Assert.True(exists);
    }

    [Then("回應內容的 message 應與已註冊會員申請成功時的回應內容完全相同")]
    public void Then回應內容的Message應與已註冊會員申請成功時的回應內容完全相同()
    {
        Assert.NotNull(this._forgotPasswordResponse);
        Assert.Equal(ExpectedForgotPasswordMessage, this._forgotPasswordResponse!.Message);
    }

    [Given("該會員已於 (\\d+) 秒前申請過忘記密碼")]
    public Task Given該會員已於秒前申請過忘記密碼(int secondsAgo)
    {
        return this.SeedPasswordResetTokenForCurrentMemberAsync(Guid.NewGuid().ToString("N"), secondsAgo);
    }

    [Given("該會員已於 (\\d+) 秒前申請過忘記密碼並取得驗證權杖 \"([^\"]*)\"")]
    public Task Given該會員已於秒前申請過忘記密碼並取得驗證權杖(int secondsAgo, string token)
    {
        return this.SeedPasswordResetTokenForCurrentMemberAsync(token, secondsAgo);
    }

    [Then("驗證權杖 \"([^\"]*)\" 應已失效")]
    public async Task Then驗證權杖應已失效(string token)
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var tokenHash = VerificationTokenHasher.Hash(token);
        var entity = await dbContext.VerificationTokens.SingleAsync(t => t.TokenHash == tokenHash);
        Assert.NotNull(entity.UsedAt);
    }

    [Given("系統已存在一筆狀態為 Active 的會員及其尚未使用的有效重設密碼權杖 \"([^\"]*)\"，Email 為 \"([^\"]*)\"")]
    public async Task Given系統已存在一筆狀態為Active的會員及其尚未使用的有效重設密碼權杖(string token, string email)
    {
        var member = await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberStatus.Active);
        await this.AddPasswordResetTokenAsync(member.Id, token, expired: false, used: false);
        scenarioContext.Set(email, "email");
    }

    [Given("系統已存在一筆狀態為 Active 的會員及其已過期或已使用的重設密碼權杖 \"([^\"]*)\"，Email 為 \"([^\"]*)\"")]
    public async Task Given系統已存在一筆狀態為Active的會員及其已過期或已使用的重設密碼權杖(string token, string email)
    {
        var member = await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberStatus.Active);
        await this.AddPasswordResetTokenAsync(member.Id, token, expired: true, used: false);
        scenarioContext.Set(email, "email");
    }

    [Given("該會員已取得尚未使用的有效重設密碼權杖 \"([^\"]*)\"")]
    public async Task Given該會員已取得尚未使用的有效重設密碼權杖(string token)
    {
        var email = scenarioContext.Get<string>("email");
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await dbContext.Members.SingleAsync(m => m.Email == email);
        await this.AddPasswordResetTokenAsync(member.Id, token, expired: false, used: false);
    }

    [When("使用者攜帶驗證權杖 \"([^\"]*)\" 設定新密碼 \"([^\"]*)\" 並確認密碼 \"([^\"]*)\" 呼叫重設密碼 API")]
    public async Task When使用者攜帶驗證權杖設定新密碼並確認密碼呼叫重設密碼Api(string token, string newPassword, string confirmPassword)
    {
        scenarioContext.Set(token, "verificationToken");

        var request = new ResetPasswordRequest(token, newPassword, confirmPassword);
        this._response = await testBase.Client.PostAsJsonAsync("/api/v1/auth/reset-password", request);
        testBase.LastResponse = this._response;

        if (this._response.StatusCode != HttpStatusCode.OK)
        {
            testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(JsonOptions);
        }
    }

    [Then("該會員應可使用新密碼 \"([^\"]*)\" 成功登入")]
    public async Task Then該會員應可使用新密碼成功登入(string newPassword)
    {
        var email = scenarioContext.Get<string>("email");
        var request = new LoginRequest(email, newPassword, null);
        using var response = await testBase.Client.PostAsJsonAsync("/api/v1/auth/login", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [When("使用者攜帶重設密碼前的 Session Cookie 呼叫登出 API")]
    public async Task When使用者攜帶重設密碼前的SessionCookie呼叫登出Api()
    {
        var sessionCookie = scenarioContext.Get<string>("sessionCookie");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", sessionCookie);
        this._response = await testBase.Client.SendAsync(request);
        testBase.LastResponse = this._response;
    }

    private async Task SeedPasswordResetTokenForCurrentMemberAsync(string token, int secondsAgo)
    {
        var email = scenarioContext.Get<string>("email");
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await dbContext.Members.SingleAsync(m => m.Email == email);

        var createdAt = testBase.TimeProvider.GetUtcNow() - TimeSpan.FromSeconds(secondsAgo);
        dbContext.VerificationTokens.Add(new VerificationToken
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            TokenHash = VerificationTokenHasher.Hash(token),
            Purpose = VerificationTokenPurpose.PasswordReset,
            CreatedAt = createdAt,
            ExpiresAt = createdAt.AddMinutes(15),
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task AddPasswordResetTokenAsync(Guid memberId, string token, bool expired, bool used)
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var now = testBase.TimeProvider.GetUtcNow();
        var tokenHash = VerificationTokenHasher.Hash(token);

        // ponytail: find-or-create，同一組固定 Token 字面值可能被 Scenario Outline 的不同 Example 重複使用，
        // 直接 Insert 會撞 token_hash 唯一索引；重複使用時就地重設狀態即可。
        var existing = await dbContext.VerificationTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash);
        if (existing is not null)
        {
            existing.MemberId = memberId;
            existing.CreatedAt = now;
            existing.ExpiresAt = expired ? now.AddMinutes(-1) : now.AddMinutes(15);
            existing.UsedAt = used ? now : null;
        }
        else
        {
            dbContext.VerificationTokens.Add(new VerificationToken
            {
                Id = Guid.NewGuid(),
                MemberId = memberId,
                TokenHash = tokenHash,
                Purpose = VerificationTokenPurpose.PasswordReset,
                CreatedAt = now,
                ExpiresAt = expired ? now.AddMinutes(-1) : now.AddMinutes(15),
                UsedAt = used ? now : null,
            });
        }

        await dbContext.SaveChangesAsync();
    }
}
