using Testcontainers.PostgreSql;

namespace MemberApi.Tests.Support;

/// <summary>
/// 以 Testcontainers 啟動真實 PostgreSQL 容器，並建立指向該容器的 WebApplicationFactory。
/// 供各情境（Scenario）的 Step Definitions 於 BeforeScenario/AfterScenario 掛接。
/// </summary>
public class PostgreSqlTestBase
{
    private PostgreSqlContainer? _container;
    private MemberApiWebApplicationFactory? _factory;

    public HttpClient Client { get; private set; } = null!;

    public async Task StartAsync()
    {
        this._container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("member_api_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await this._container.StartAsync();

        this._factory = new MemberApiWebApplicationFactory(this._container.GetConnectionString());
        this.Client = this._factory.CreateClient();
    }

    public async Task StopAsync()
    {
        this.Client?.Dispose();

        if (this._factory is not null)
        {
            await this._factory.DisposeAsync();
        }

        if (this._container is not null)
        {
            await this._container.DisposeAsync();
        }
    }
}
