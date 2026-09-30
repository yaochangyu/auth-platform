using AuthServer.Infrastructure;
using AuthServer.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;

namespace AuthServer.Tests.Steps;

[Binding]
public class MemberDirectorySteps(AuthServerTestBase testBase)
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset VerifiedAt = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    private Guid _memberId = Guid.NewGuid();
    private string _stamp = string.Empty;
    private string _role = string.Empty;
    private MemberSnapshot? _snapshot;

    [Given("資料庫中有一位已驗證 Email 的管理員會員")]
    public Task Given管理員() => this.InsertAsync("admin", VerifiedAt);

    [Given("資料庫中有一位尚未驗證 Email 的一般會員")]
    public Task Given一般() => this.InsertAsync("member", null);

    [When("以會員目錄查詢該會員")]
    public Task When查詢() => this.QueryAsync(this._memberId);

    [When("以會員目錄查詢一位不存在的會員")]
    public Task When查詢不存在() => this.QueryAsync(Guid.NewGuid());

    [Then("快照應包含該會員的 Security Stamp、角色、Email、顯示名稱與驗證狀態")]
    public void Then完整()
    {
        var snapshot = this._snapshot!;
        Assert.Equal(this._stamp, snapshot.SecurityStamp);
        Assert.Equal(this._role, snapshot.Role);
        Assert.Equal($"{this._memberId:N}@example.com", snapshot.Email);
        Assert.Equal("目錄測試會員", snapshot.DisplayName);
        Assert.True(snapshot.EmailVerified);
    }

    [Then("快照的更新時間應為 Email 驗證時間")]
    public void Then驗證時間() => Assert.Equal(VerifiedAt, this._snapshot!.UpdatedAt);

    [Then("快照的 EmailVerified 應為 false")]
    public void Then未驗證() => Assert.False(this._snapshot!.EmailVerified);

    [Then("快照的更新時間應為建立時間")]
    public void Then建立時間() => Assert.Equal(CreatedAt, this._snapshot!.UpdatedAt);

    [Then("應回傳空值")]
    public void Then空值() => Assert.Null(this._snapshot);

    private async Task QueryAsync(Guid memberId)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        this._snapshot = await scope.ServiceProvider.GetRequiredService<IMemberDirectory>().GetAsync(memberId);
    }

    private async Task InsertAsync(string role, DateTimeOffset? verifiedAt)
    {
        this._stamp = Guid.NewGuid().ToString("N");
        this._role = role;

        await using var connection = new NpgsqlConnection(TestRunHooks.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "insert into members (id, security_stamp, email, display_name, email_verified_at, created_at, role) "
            + "values (@id, @stamp, @email, '目錄測試會員', @verifiedAt, @createdAt, @role)";
        command.Parameters.AddWithValue("id", this._memberId);
        command.Parameters.AddWithValue("stamp", this._stamp);
        command.Parameters.AddWithValue("email", $"{this._memberId:N}@example.com");
        command.Parameters.AddWithValue("verifiedAt", (object?)verifiedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("createdAt", CreatedAt);
        command.Parameters.AddWithValue("role", role);
        await command.ExecuteNonQueryAsync();
    }
}
