using AdminApi.Entities;
using AdminApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using Testcontainers.PostgreSql;

namespace AdminApi.Tests.Support;

// 只建立 developer-api 與 auth-server 擁有的表（正式環境由它們的 migration 建立），不含 audit_logs。
public class OtherServicesSchemaContext(DbContextOptions<OtherServicesSchemaContext> options) : AdminApiDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Model.RemoveEntityType(typeof(AuditLog));
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetIsTableExcludedFromMigrations(false);
        }
    }
}

[Binding]
public static class TestRunHooks
{
    private static PostgreSqlContainer? _container;

    public static string ConnectionString { get; private set; } = string.Empty;

    [BeforeTestRun]
    public static async Task StartContainerAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("admin_api_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        var otherOptions = new DbContextOptionsBuilder<OtherServicesSchemaContext>()
            .UseNpgsql(ConnectionString).UseSnakeCaseNamingConvention().UseOpenIddict().Options;
        await using (var others = new OtherServicesSchemaContext(otherOptions))
        {
            await others.Database.EnsureCreatedAsync();
        }

        // 用真正的 migration 建立 audit_logs 與不可篡改觸發器，migration 本身也一併被測到。
        var options = new DbContextOptionsBuilder<AdminApiDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable(AdminApiDbContext.MigrationsHistoryTable))
            .UseSnakeCaseNamingConvention().UseOpenIddict().Options;
        await using var dbContext = new AdminApiDbContext(options);
        await dbContext.Database.MigrateAsync();
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
