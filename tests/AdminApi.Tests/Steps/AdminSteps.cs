using System.Security.Cryptography;
using AdminApi.Entities;
using AdminApi.Infrastructure;
using AdminApi.Tests.Support;
using AuthShared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenIddict.Abstractions;
using Reqnroll;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AdminApi.Tests.Steps;

[Binding]
public class AdminSteps(AdminApiTestBase testBase)
{
    private const string ApplicationsUrl = "/api/v1/admin/applications";
    private const string AuditLogsUrl = "/api/v1/admin/audit-logs";

    private readonly Dictionary<string, (int Authorizations, int Tokens)> _oauthSeeds = [];
    private string _auditProject = string.Empty;
    private bool _databaseRejected;

    // ---- 存取控制 ----

    [When("以 (.*) 查詢全平台應用程式")]
    public Task When以指定Token查詢(string tokenCase)
    {
        var memberId = Guid.NewGuid().ToString();
        var token = tokenCase switch
        {
            "未帶 Access Token" => null,
            "偽造的 Access Token" => "forged.access.token",
            "已過期的 Access Token" => TestJwtIssuer.Create(memberId, lifetime: TimeSpan.FromMinutes(-10)),
            "使用未知金鑰簽署的 Access Token" => TestJwtIssuer.Create(memberId, signingKey: new RsaSecurityKey(RSA.Create(2048)) { KeyId = "test-key" }),
            "一般會員（role 為 member）的 Access Token" => TestJwtIssuer.Create(memberId, role: "member"),
            "沒有 role 的 Access Token" => TestJwtIssuer.Create(memberId, role: null),
            "有 admin 角色但缺少 admin_api 範疇的 Access Token" => TestJwtIssuer.Create(memberId, scope: "openid profile"),
            "sub 不是有效會員識別碼的 Access Token" => TestJwtIssuer.Create("not-a-member-id"),
            _ => throw new ArgumentOutOfRangeException(nameof(tokenCase), tokenCase, "未知的 Token 情況"),
        };
        return testBase.SendAsync(HttpMethod.Get, ApplicationsUrl, token);
    }

    [When("一般會員 \"([^\"]*)\" 嘗試(查詢應用程式|查看應用程式詳情|停用應用程式|查詢稽核紀錄)")]
    public Task When一般會員嘗試(string member, string action)
    {
        var token = TestJwtIssuer.Create(testBase.MemberId(member).ToString(), role: "member");
        var id = Guid.NewGuid();
        return action switch
        {
            "查詢應用程式" => testBase.SendAsync(HttpMethod.Get, ApplicationsUrl, token),
            "查看應用程式詳情" => testBase.SendAsync(HttpMethod.Get, $"{ApplicationsUrl}/{id}", token),
            "停用應用程式" => testBase.SendAsync(HttpMethod.Put, $"{ApplicationsUrl}/{id}/status", token, new { status = "Suspended", reason = "入侵" }),
            _ => testBase.SendAsync(HttpMethod.Get, AuditLogsUrl, token),
        };
    }

    // ---- 情境資料 ----

    [Given("平台上有應用專案 \"([^\"]*)\"，擁有者 \"([^\"]*)\"，狀態 \"([^\"]*)\"")]
    public async Task Given有專案(string name, string owner, string status)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AdminApiDbContext>();
        var now = testBase.Factory.TimeProvider.GetUtcNow();
        var application = new ManagedApplication
        {
            Id = Guid.NewGuid(),
            OwnerMemberId = testBase.MemberId(owner),
            ClientId = Guid.NewGuid().ToString("N"),
            Name = name,
            Description = "測試用簡介",
            ContactEmail = "dev@example.com",
            Status = Enum.Parse<ApplicationStatus>(status),
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync();
        testBase.Applications[name] = (application.Id, application.ClientId);
    }

    [Given("專案 \"([^\"]*)\" 有 (\\d+) 把有效的 API Key")]
    public async Task Given有ApiKey(string project, int count)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AdminApiDbContext>();
        for (var i = 0; i < count; i++)
        {
            dbContext.ApiKeys.Add(new ManagedApiKey { Id = Guid.NewGuid(), ApplicationId = testBase.Applications[project].Id });
        }

        await dbContext.SaveChangesAsync();
    }

    [Given("專案 \"([^\"]*)\" 已建立 OAuth Client，並有 (\\d+) 筆授權與 (\\d+) 個 Token")]
    public async Task Given有OAuthClient(string project, int authorizationCount, int tokenCount)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var clients = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();

        var client = await clients.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = testBase.Applications[project].ClientId,
            DisplayName = project,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            Permissions = { Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.GrantTypes.AuthorizationCode, Permissions.ResponseTypes.Code },
        });
        var clientId = (await clients.GetIdAsync(client))!;
        var now = testBase.Factory.TimeProvider.GetUtcNow();

        for (var i = 0; i < authorizationCount; i++)
        {
            await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = clientId,
                Subject = Guid.NewGuid().ToString(),
                Status = Statuses.Valid,
                Type = AuthorizationTypes.Permanent,
                CreationDate = now,
            });
        }

        for (var i = 0; i < tokenCount; i++)
        {
            await tokens.CreateAsync(new OpenIddictTokenDescriptor
            {
                ApplicationId = clientId,
                Subject = Guid.NewGuid().ToString(),
                Status = Statuses.Valid,
                Type = TokenTypeHints.RefreshToken,
                CreationDate = now,
                ExpirationDate = now.AddDays(30),
            });
        }

        this._oauthSeeds[project] = (authorizationCount, tokenCount);
    }

    [Given("專案 \"([^\"]*)\" 已建立 OAuth Client，且類型為 \"([^\"]*)\"")]
    public async Task Given已建立OAuthClient且類型為(string project, string clientType)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var clientId = testBase.Applications[project].ClientId;
        var existing = await applications.FindByClientIdAsync(clientId);
        var type = clientType == "Public" ? ClientTypes.Public : ClientTypes.Confidential;
        if (existing is not null)
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await applications.PopulateAsync(descriptor, existing);
            descriptor.ClientType = type;
            await applications.UpdateAsync(existing, descriptor);
        }
        else
        {
            await applications.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                DisplayName = project,
                ClientType = type,
            });
        }
    }

    // ---- 操作 ----

    [When("管理員 \"([^\"]*)\" 查詢全平台應用程式，狀態篩選 \"([^\"]*)\"")]
    public Task When查詢全平台(string admin, string status) =>
        testBase.SendAsync(HttpMethod.Get, $"{ApplicationsUrl}?pageSize=100{(status.Length == 0 ? "" : $"&status={status}")}", testBase.AdminToken(admin));

    [When("管理員 \"([^\"]*)\" 查看專案 \"([^\"]*)\" 的詳情")]
    public Task When查看詳情(string admin, string project) =>
        testBase.SendAsync(HttpMethod.Get, $"{ApplicationsUrl}/{testBase.Applications[project].Id}", testBase.AdminToken(admin));

    [When("管理員 \"([^\"]*)\" 查看不存在的應用程式")]
    public Task When查看不存在(string admin) =>
        testBase.SendAsync(HttpMethod.Get, $"{ApplicationsUrl}/{Guid.NewGuid()}", testBase.AdminToken(admin));

    [When("管理員 \"([^\"]*)\" 將專案 \"([^\"]*)\" 的狀態改為 \"([^\"]*)\"，原因 \"([^\"]*)\"")]
    public Task When變更狀態(string admin, string project, string status, string reason) =>
        testBase.SendAsync(
            HttpMethod.Put,
            $"{ApplicationsUrl}/{testBase.Applications[project].Id}/status",
            testBase.AdminToken(admin),
            new { status, reason = reason.Length == 0 ? null : reason });

    [When("管理員 \"([^\"]*)\" 經由反向代理將專案 \"([^\"]*)\" 的狀態改為 \"([^\"]*)\"，原因 \"([^\"]*)\"，客戶端 IP 為 \"([^\"]*)\"")]
    public Task When經由代理變更狀態(string admin, string project, string status, string reason, string clientIp) =>
        testBase.SendAsync(
            HttpMethod.Put,
            $"{ApplicationsUrl}/{testBase.Applications[project].Id}/status",
            testBase.AdminToken(admin),
            new { status, reason = reason.Length == 0 ? null : reason },
            new Dictionary<string, string> { ["X-Forwarded-For"] = clientIp });

    [Given("管理員 \"([^\"]*)\" 已將專案 \"([^\"]*)\" 停用")]
    public async Task Given已停用(string admin, string project)
    {
        await this.When變更狀態(admin, project, "Suspended", "測試用停用");
        Assert.Equal(200, (int)testBase.LastResponse!.StatusCode);
    }

    [Given("管理員 \"([^\"]*)\" 已將專案 \"([^\"]*)\" 取消停用")]
    public async Task Given已取消停用(string admin, string project)
    {
        await this.When變更狀態(admin, project, "Active", "測試用取消停用");
        Assert.Equal(200, (int)testBase.LastResponse!.StatusCode);
    }

    [When("管理員 \"([^\"]*)\" 查詢專案 \"([^\"]*)\" 的稽核紀錄")]
    public Task When查詢稽核(string admin, string project) => this.QueryAuditAsync(admin, project, string.Empty);

    [When("管理員 \"([^\"]*)\" 查詢專案 \"([^\"]*)\" 的稽核紀錄，動作 \"([^\"]*)\"")]
    public Task When查詢稽核依動作(string admin, string project, string action) => this.QueryAuditAsync(admin, project, $"&action={action}");

    [When("管理員 \"([^\"]*)\" 查詢專案 \"([^\"]*)\" 的稽核紀錄，每頁 (\\d+) 筆、第 (\\d+) 頁")]
    public Task When查詢稽核分頁(string admin, string project, int pageSize, int page) => this.QueryAuditAsync(admin, project, $"&pageSize={pageSize}&page={page}");

    [When("管理員 \"([^\"]*)\" 查詢操作人為 \"([^\"]*)\" 的稽核紀錄")]
    public Task When依操作人查詢(string admin, string actor) =>
        testBase.SendAsync(HttpMethod.Get, $"{AuditLogsUrl}?actorMemberId={testBase.MemberId(actor)}", testBase.AdminToken(admin));

    [When("直接對資料庫執行 (\\w+)")]
    public async Task When直接操作資料庫(string operation)
    {
        var sql = operation switch
        {
            "UPDATE" => "update audit_logs set action = 'tampered'",
            "DELETE" => "delete from audit_logs",
            _ => "truncate table audit_logs",
        };

        await using var connection = new NpgsqlConnection(TestRunHooks.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.RestrictViolation)
        {
            this._databaseRejected = true;
        }
    }

    // ---- 驗證：應用程式 ----

    [Then("清單應包含專案 \"([^\"]*)\"")]
    public void Then清單包含(string projects) => Assert.All(projects.Split('、'), project => Assert.Contains(testBase.Applications[project].Id, ListedIds()));

    [Then("清單應包含專案 \"([^\"]*)\" 且不包含專案 \"([^\"]*)\"")]
    public void Then清單包含且不包含(string included, string excluded)
    {
        Assert.Contains(testBase.Applications[included].Id, ListedIds());
        Assert.DoesNotContain(testBase.Applications[excluded].Id, ListedIds());
        Assert.All(testBase.LastBody.GetProperty("items").EnumerateArray(), item => Assert.Equal("PendingReview", item.GetProperty("status").GetString()));
    }

    [Then("詳情的擁有者應為 \"([^\"]*)\"")]
    public void Then擁有者(string owner) => Assert.Equal(testBase.MemberId(owner), testBase.LastBody.GetProperty("ownerMemberId").GetGuid());

    [Then("詳情的客戶端類型應為 \"([^\"]*)\"")]
    public void Then客戶端類型(string expectedType) =>
        Assert.Equal(expectedType, testBase.LastBody.GetProperty("clientType").GetString());

    [Then("回應的專案狀態應為 \"([^\"]*)\"")]
    public void Then專案狀態(string status) =>
        Assert.Equal(status, testBase.LastBody.GetProperty("application").GetProperty("status").GetString());

    // ---- 驗證：斷路器 ----

    [Then("斷路器回報作廢 (\\d+) 把 API Key、(\\d+) 筆授權、(\\d+) 個 Token")]
    public void Then斷路器回報(int keys, int authorizations, int tokens)
    {
        var breaker = testBase.LastBody.GetProperty("circuitBreaker");
        Assert.Equal(keys, breaker.GetProperty("revokedApiKeys").GetInt32());
        Assert.Equal(authorizations, breaker.GetProperty("revokedAuthorizations").GetInt32());
        Assert.Equal(tokens, breaker.GetProperty("revokedTokens").GetInt32());
    }

    [Then("專案 \"([^\"]*)\" 的所有 API Key 都已撤銷")]
    public async Task Then所有ApiKey已撤銷(string project)
    {
        var keys = await this.LoadApiKeysAsync(project);
        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.NotNull(key.RevokedAt));
    }

    [Then("專案 \"([^\"]*)\" 的所有授權與 Token 都已撤銷")]
    public async Task Then所有授權與Token已撤銷(string project)
    {
        var (authorizations, tokens) = await this.LoadOAuthStatusesAsync(project);
        Assert.Equal(this._oauthSeeds[project].Authorizations, authorizations.Count);
        Assert.Equal(this._oauthSeeds[project].Tokens, tokens.Count);
        Assert.All(authorizations.Concat(tokens), status => Assert.Equal(Statuses.Revoked, status));
    }

    [Then("專案 \"([^\"]*)\" 的 API Key、授權與 Token 都仍然有效")]
    public async Task Then仍然有效(string project)
    {
        var keys = await this.LoadApiKeysAsync(project);
        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.Null(key.RevokedAt));

        var (authorizations, tokens) = await this.LoadOAuthStatusesAsync(project);
        Assert.NotEmpty(authorizations.Concat(tokens));
        Assert.All(authorizations.Concat(tokens), status => Assert.Equal(Statuses.Valid, status));
    }

    [Then("專案 \"([^\"]*)\" 的 OAuth Client 已被標記為停用")]
    public async Task ThenClient已停用(string project) => Assert.True(await this.IsClientSuspendedAsync(project));

    [Then("專案 \"([^\"]*)\" 的 OAuth Client 未被標記為停用")]
    public async Task ThenClient未停用(string project) => Assert.False(await this.IsClientSuspendedAsync(project));

    // ---- 驗證：稽核紀錄 ----

    [Then("稽核紀錄應有 (\\d+) 筆")]
    public void Then稽核筆數(int count) => Assert.Equal(count, testBase.LastBody.GetProperty("total").GetInt32());

    [Then("回應的總筆數應為 (\\d+)")]
    public void Then總筆數(int total) => Assert.Equal(total, testBase.LastBody.GetProperty("total").GetInt32());

    [Then("目前頁面應有 (\\d+) 筆稽核紀錄")]
    public void Then頁面筆數(int count) => Assert.Equal(count, testBase.LastBody.GetProperty("items").GetArrayLength());

    [Then("最新一筆稽核紀錄的操作人應為管理員 \"([^\"]*)\"")]
    public void Then操作人(string admin) => Assert.Equal(testBase.MemberId(admin), Latest().GetProperty("actorMemberId").GetGuid());

    [Then("所有稽核紀錄的操作人都應為管理員 \"([^\"]*)\"")]
    public void Then所有操作人(string admin)
    {
        Assert.NotEmpty(testBase.LastBody.GetProperty("items").EnumerateArray());
        Assert.All(testBase.LastBody.GetProperty("items").EnumerateArray(), item => Assert.Equal(testBase.MemberId(admin), item.GetProperty("actorMemberId").GetGuid()));
    }

    [Then("最新一筆稽核紀錄的動作應為 \"([^\"]*)\"，目標為該專案")]
    public void Then動作與目標(string action)
    {
        Assert.Equal(action, Latest().GetProperty("action").GetString());
        Assert.Equal("application", Latest().GetProperty("targetType").GetString());
        Assert.Equal(testBase.Applications[this._auditProject].Id.ToString(), Latest().GetProperty("targetId").GetString());
    }

    [Then("最新一筆稽核紀錄的客戶端 IP 應為 \"([^\"]*)\"")]
    public void ThenIp(string ip) => Assert.Equal(ip, Latest().GetProperty("clientIp").GetString());

    [Then("最新一筆稽核紀錄的變更前後狀態應為 \"([^\"]*)\" 與 \"([^\"]*)\"")]
    public void Then前後狀態(string before, string after)
    {
        Assert.Equal(before, Latest().GetProperty("before").GetProperty("status").GetString());
        Assert.Equal(after, Latest().GetProperty("after").GetProperty("status").GetString());
    }

    [Then("最新一筆稽核紀錄的補充資訊應包含原因 \"([^\"]*)\"")]
    public void Then補充資訊(string reason)
    {
        Assert.Equal(reason, Latest().GetProperty("details").GetProperty("reason").GetString());
        Assert.Equal(testBase.Applications[this._auditProject].ClientId, Latest().GetProperty("details").GetProperty("clientId").GetString());
    }

    [Then("資料庫應拒絕該操作")]
    public void Then資料庫拒絕() => Assert.True(this._databaseRejected, "資料庫應以 restrict_violation 拒絕對 audit_logs 的修改");

    // ---- 輔助 ----

    private Task QueryAuditAsync(string admin, string project, string extraQuery)
    {
        this._auditProject = project;
        return testBase.SendAsync(
            HttpMethod.Get, $"{AuditLogsUrl}?targetId={testBase.Applications[project].Id}{extraQuery}", testBase.AdminToken(admin));
    }

    private System.Text.Json.JsonElement Latest() => testBase.LastBody.GetProperty("items")[0];

    private List<Guid> ListedIds() =>
        [.. testBase.LastBody.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    private async Task<List<ManagedApiKey>> LoadApiKeysAsync(string project)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AdminApiDbContext>();
        var id = testBase.Applications[project].Id;
        return await dbContext.ApiKeys.AsNoTracking().Where(key => key.ApplicationId == id).ToListAsync();
    }

    private async Task<(List<string?> Authorizations, List<string?> Tokens)> LoadOAuthStatusesAsync(string project)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var clients = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();

        var client = (await clients.FindByClientIdAsync(testBase.Applications[project].ClientId))!;
        var clientId = (await clients.GetIdAsync(client))!;

        var authorizationStatuses = new List<string?>();
        await foreach (var authorization in authorizations.FindByApplicationIdAsync(clientId))
        {
            authorizationStatuses.Add(await authorizations.GetStatusAsync(authorization));
        }

        var tokenStatuses = new List<string?>();
        await foreach (var token in tokens.FindByApplicationIdAsync(clientId))
        {
            tokenStatuses.Add(await tokens.GetStatusAsync(token));
        }

        return (authorizationStatuses, tokenStatuses);
    }

    private async Task<bool> IsClientSuspendedAsync(string project)
    {
        await using var scope = testBase.Factory.Services.CreateAsyncScope();
        var clients = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var client = (await clients.FindByClientIdAsync(testBase.Applications[project].ClientId))!;
        var properties = await clients.GetPropertiesAsync(client);
        return properties.TryGetValue(ClientProperties.Suspended, out var suspended) && suspended.ValueKind == System.Text.Json.JsonValueKind.True;
    }
}
