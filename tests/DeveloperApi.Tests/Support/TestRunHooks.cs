using DeveloperApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
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
