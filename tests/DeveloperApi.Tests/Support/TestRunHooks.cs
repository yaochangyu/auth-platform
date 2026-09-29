using DeveloperApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Reqnroll;
using Testcontainers.PostgreSql;

namespace DeveloperApi.Tests.Support;

[Binding]
public static class TestRunHooks
{
    private static PostgreSqlContainer? _container;

    public static string ConnectionString { get; private set; } = string.Empty;

    [BeforeTestRun]
    public static async Task StartContainerAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("developer_api_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        var options = new DbContextOptionsBuilder<DeveloperApiDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable(DeveloperApiDbContext.MigrationsHistoryTable))
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var dbContext = new DeveloperApiDbContext(options);
        await dbContext.Database.MigrateAsync();

        // OpenIddict 資料表正式環境由 auth-server 的 migration 建立；developer-api 只讀寫，測試在此自行建立。
        var storeOptions = new DbContextOptionsBuilder<OpenIddictStoreContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .UseOpenIddict()
            .Options;
        await using var storeContext = new OpenIddictStoreContext(storeOptions);
        await storeContext.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
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
