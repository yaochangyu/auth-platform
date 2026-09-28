using MemberApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using Testcontainers.PostgreSql;

namespace MemberApi.Tests.Support;

[Binding]
public static class TestRunHooks
{
    private static PostgreSqlContainer? _container;

    public static string ConnectionString { get; private set; } = string.Empty;

    [BeforeTestRun]
    public static async Task StartContainerAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("member_api_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        var options = new DbContextOptionsBuilder<MemberApiDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var dbContext = new MemberApiDbContext(options);
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
