using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using MemberApi.Security;
using MemberApi.Tests.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace MemberApi.Tests.Steps;

// ponytail: 整個 Test Run 共用同一個容器化資料庫、情境間不重置，
// 因此各情境需使用不重複的 Email/Token 避免互相污染；資料量大時可改用 Respawn 做情境級重置。
[Binding]
public class RegistrationSteps(PostgreSqlTestBase testBase)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private HttpResponseMessage _response = null!;
    private RegisterResponse? _registerResponse;
    private VerifyEmailResponse? _verifyEmailResponse;
    private string? _seededEmail;
    private string? _originalPasswordHash;

    [When("使用者以下列資料呼叫註冊 API")]
    public async Task When使用者以下列資料呼叫註冊Api(DataTable table)
    {
        var row = table.Rows[0];
        await this.PostRegisterAsync(row["email"], row["password"], row["confirmPassword"], row["displayName"]);
    }

    [Given("系統已存在一筆 Email 為 \"([^\"]*)\" 且狀態為 Pending 的會員")]
    public async Task Given系統已存在一筆EmailStatusPending的會員(string email)
    {
        var member = await MemberSeeder.SeedMemberAsync(testBase.Factory, email, MemberStatus.Pending);
        this._seededEmail = email;
        this._originalPasswordHash = member.PasswordHash;
    }

    [When("使用者以相同 Email 但不同密碼再次呼叫註冊 API")]
    public async Task When使用者以相同Email但不同密碼再次呼叫註冊Api()
    {
        await this.PostRegisterAsync(this._seededEmail!, "Different@Passw0rd2", "Different@Passw0rd2", "測試會員");
    }

    [Given("系統已存在一筆狀態為 Pending 的會員及其尚未使用的有效驗證權杖 \"([^\"]*)\"")]
    public async Task Given系統已存在一筆狀態為Pending的會員及其尚未使用的有效驗證權杖(string token)
    {
        await this.SeedMemberWithTokenAsync("verify-success@1111.com.tw", token, expired: false);
    }

    [Given("系統已存在一筆已過期或已使用的驗證權杖 \"([^\"]*)\"")]
    public async Task Given系統已存在一筆已過期或已使用的驗證權杖(string token)
    {
        await this.SeedMemberWithTokenAsync("verify-expired@1111.com.tw", token, expired: true);
    }

    [Given("系統已存在一筆狀態為 Active 的會員及其尚未使用的有效驗證權杖 \"([^\"]*)\"")]
    public async Task Given系統已存在一筆狀態為Active的會員及其尚未使用的有效驗證權杖(string token)
    {
        await this.SeedMemberWithTokenAsync("verify-active@1111.com.tw", token, expired: false, status: MemberStatus.Active);
    }

    [When("使用者攜帶驗證權杖 \"([^\"]*)\" 呼叫 Email 驗證 API")]
    public async Task When使用者攜帶驗證權杖呼叫Email驗證Api(string token)
    {
        await this.PostVerifyEmailAsync(token);
    }

    [When("使用者攜帶空白驗證權杖呼叫 Email 驗證 API")]
    public async Task When使用者攜帶空白驗證權杖呼叫Email驗證Api()
    {
        await this.PostVerifyEmailAsync(string.Empty);
    }

    [Then("回應內容的會員狀態應為 \"([^\"]*)\"")]
    public void Then回應內容的會員狀態應為(string status)
    {
        var actual = this._registerResponse?.Status ?? this._verifyEmailResponse?.Status;
        Assert.NotNull(actual);
        Assert.Equal(status, actual.ToString());
    }

    [Then("系統應為該會員寫入一筆尚未使用的驗證權杖")]
    public async Task Then系統應為該會員寫入一筆尚未使用的驗證權杖()
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var exists = await dbContext.VerificationTokens.AnyAsync(token =>
            token.MemberId == this._registerResponse!.MemberId && token.UsedAt == null);
        Assert.True(exists);
    }

    [Then("系統應為該會員寫入一筆待發送之驗證信 Outbox 訊息")]
    public async Task Then系統應為該會員寫入一筆待發送之驗證信Outbox訊息()
    {
        await this.AssertOutboxMessageExistsAsync(this._registerResponse!.Email);
    }

    [Then("系統應為該會員派發一筆新的驗證信 Outbox 訊息")]
    public async Task Then系統應為該會員派發一筆新的驗證信Outbox訊息()
    {
        await this.AssertOutboxMessageExistsAsync(this._seededEmail!);
    }

    [Then("錯誤內容應包含 \"([^\"]*)\" 欄位的錯誤訊息")]
    public void Then錯誤內容應包含欄位的錯誤訊息(string field)
    {
        Assert.NotNull(testBase.LastProblemDetails);
        Assert.True(testBase.LastProblemDetails!.Errors.ContainsKey(field));
    }

    [Then("該會員的密碼雜湊應維持原始值未被覆蓋")]
    public async Task Then該會員的密碼雜湊應維持原始值未被覆蓋()
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var member = await dbContext.Members.SingleAsync(m => m.Email == this._seededEmail);
        Assert.Equal(this._originalPasswordHash, member.PasswordHash);
    }

    [Then("該驗證權杖應被標記為已使用")]
    public async Task Then該驗證權杖應被標記為已使用()
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var token = await dbContext.VerificationTokens.SingleAsync(t => t.MemberId == this._verifyEmailResponse!.MemberId);
        Assert.NotNull(token.UsedAt);
    }

    private async Task PostRegisterAsync(string email, string password, string confirmPassword, string displayName)
    {
        var request = new RegisterRequest(email, password, confirmPassword, displayName);
        this._response = await testBase.Client.PostAsJsonAsync("/api/v1/auth/register", request);
        testBase.LastResponse = this._response;

        if (this._response.StatusCode == HttpStatusCode.Created)
        {
            this._registerResponse = await this._response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions);
        }
        else
        {
            testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        }
    }

    private async Task PostVerifyEmailAsync(string token)
    {
        var request = new VerifyEmailRequest(token);
        this._response = await testBase.Client.PostAsJsonAsync("/api/v1/auth/verify-email", request);
        testBase.LastResponse = this._response;

        if (this._response.StatusCode == HttpStatusCode.OK)
        {
            this._verifyEmailResponse = await this._response.Content.ReadFromJsonAsync<VerifyEmailResponse>(JsonOptions);
        }
        else
        {
            testBase.LastProblemDetails = await this._response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        }
    }

    private async Task SeedMemberWithTokenAsync(
        string email,
        string token,
        bool expired,
        MemberStatus status = MemberStatus.Pending)
    {
        var member = await MemberSeeder.SeedMemberAsync(testBase.Factory, email, status);

        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();

        var now = DateTimeOffset.UtcNow;
        dbContext.VerificationTokens.Add(new VerificationToken
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            TokenHash = VerificationTokenHasher.Hash(token),
            ExpiresAt = expired ? now.AddHours(-1) : now.AddHours(1),
            CreatedAt = now,
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task AssertOutboxMessageExistsAsync(string email)
    {
        using var scope = testBase.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberApiDbContext>();
        var exists = await dbContext.OutboxMessages.AnyAsync(message => message.ToEmail == email);
        Assert.True(exists);
    }
}
