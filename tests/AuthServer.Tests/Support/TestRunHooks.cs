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
