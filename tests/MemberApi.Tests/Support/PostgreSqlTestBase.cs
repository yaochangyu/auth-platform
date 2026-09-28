namespace MemberApi.Tests.Support;

/// <summary>
/// 每個情境（Scenario）建立指向共用 PostgreSQL 容器（見 TestRunHooks）的 WebApplicationFactory。
/// 容器本身於整個 Test Run 只啟動一次，此類別只負責情境層級的測試伺服器生命週期。
/// </summary>
public class PostgreSqlTestBase
{
    private MemberApiWebApplicationFactory? _factory;

    public HttpClient Client { get; private set; } = null!;

    public MemberApiWebApplicationFactory Factory => this._factory!;

    public HttpResponseMessage? LastResponse { get; set; }

    public Task StartAsync()
    {
        this._factory = new MemberApiWebApplicationFactory(TestRunHooks.ConnectionString);
        this.Client = this._factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        this.Client?.Dispose();

        if (this._factory is not null)
        {
            await this._factory.DisposeAsync();
        }
    }
}
