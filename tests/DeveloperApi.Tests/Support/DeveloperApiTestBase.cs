namespace DeveloperApi.Tests.Support;

public class DeveloperApiTestBase
{
    private readonly Dictionary<string, Guid> _members = [];
    private DeveloperApiWebApplicationFactory? _factory;

    public HttpClient Client { get; private set; } = null!;

    public HttpResponseMessage? LastResponse { get; set; }

    // 每個情境的開發者 Guid 各自產生，避免平行情境共用資料庫時互相看到對方的專案。
    public Guid MemberId(string name)
    {
        if (!this._members.TryGetValue(name, out var id))
        {
            this._members[name] = id = Guid.NewGuid();
        }

        return id;
    }

    public string TokenFor(string name) => TestJwtIssuer.Create(this.MemberId(name));

    public void Start()
    {
        this._factory = new DeveloperApiWebApplicationFactory(TestRunHooks.ConnectionString);
        this.Client = this._factory.CreateClient();
    }

    public async Task StopAsync()
    {
        this.Client?.Dispose();

        if (this._factory is not null)
        {
            await this._factory.DisposeAsync();
            this._factory = null;
        }
    }
}
