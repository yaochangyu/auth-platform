using Microsoft.AspNetCore.Mvc.Testing;

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

    public Microsoft.Extensions.Time.Testing.FakeTimeProvider TimeProvider => this._factory!.TimeProvider;

    public HttpResponseMessage? LastResponse { get; set; }

    public ProblemDetailsPayload? LastProblemDetails { get; set; }

    public Task StartAsync()
    {
        this._factory = new MemberApiWebApplicationFactory(TestRunHooks.ConnectionString);

        // ponytail: Domain=.1111.com.tw 的 Cookie 在 localhost 測試主機下不符合瀏覽器同源規則，
        // 自動 CookieContainer 會靜默丟棄，改由 Steps 手動解析 Set-Cookie 標頭並帶入後續請求。
        this.Client = this._factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
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
