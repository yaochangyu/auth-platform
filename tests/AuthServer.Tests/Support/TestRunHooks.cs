using AuthServer.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using Testcontainers.PostgreSql;

namespace AuthServer.Tests.Support;

[Binding]
public static class TestRunHooks
{
    private static PostgreSqlContainer? _container;

    public static string ConnectionString { get; private set; } = string.Empty;

    [BeforeTestRun]
    public static async Task StartContainerAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("auth_server_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // 兩個 Feature 類別平行啟動伺服器，先在此完成 migration，避免同時套用互相衝突。
        var options = new DbContextOptionsBuilder<AuthServerDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable(AuthServerDbContext.MigrationsHistoryTable))
            .UseSnakeCaseNamingConvention()
            .UseOpenIddict()
            .Options;
        await using (var dbContext = new AuthServerDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        // 與 member-api 共用資料庫（ADR 0006）；Auth Server 只讀取 members 的 security_stamp 與 UserInfo 所需欄位，
        // 測試只建立這些欄位，不引用 MemberApi 專案（兩者的 Program 類別會衝突）。
        await _container.ExecScriptAsync(
            "create table members (id uuid primary key, security_stamp text not null, email text not null, display_name text not null, "
            + "email_verified_at timestamptz, created_at timestamptz not null);");
    }

    [AfterTestRun]
    public static async Task StopContainerAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
