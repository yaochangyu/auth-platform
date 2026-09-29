using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AdminApi.Tests.Support;

public class AdminApiTestBase
{
    private readonly Dictionary<string, Guid> _members = [];
    private AdminApiWebApplicationFactory? _factory;

    public HttpClient Client { get; private set; } = null!;

    public AdminApiWebApplicationFactory Factory => this._factory!;

    public HttpResponseMessage? LastResponse { get; private set; }

    public JsonElement LastBody { get; private set; }

    // 專案名稱 -> (Id, ClientId)，讓不同步驟類別都能以名稱引用情境中建立的專案。
    public Dictionary<string, (Guid Id, string ClientId)> Applications { get; } = [];

    // 每個情境的成員 Guid 各自產生，避免平行情境共用資料庫時互相干擾。
    public Guid MemberId(string name)
    {
        if (!this._members.TryGetValue(name, out var id))
        {
            this._members[name] = id = Guid.NewGuid();
        }

        return id;
    }

    public string AdminToken(string name) => TestJwtIssuer.Create(this.MemberId(name).ToString());

    public void Start()
    {
        this._factory = new AdminApiWebApplicationFactory(TestRunHooks.ConnectionString);
        this.Client = this._factory.CreateClient();
    }

    public async Task SendAsync(HttpMethod method, string url, string? token, object? body = null, IDictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
        {
            request.Headers.Add(name, value);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        this.LastResponse = await this.Client.SendAsync(request);
        var text = await this.LastResponse.Content.ReadAsStringAsync();
        this.LastBody = text.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(text);
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
