using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DeveloperApi.Tests.Support;

public class DeveloperApiTestBase
{
    private readonly Dictionary<string, Guid> _members = [];
    private DeveloperApiWebApplicationFactory? _factory;

    public HttpClient Client { get; private set; } = null!;

    public DeveloperApiWebApplicationFactory Factory => this._factory!;

    public HttpResponseMessage? LastResponse { get; set; }

    public JsonElement LastBody { get; private set; }

    public string LastBodyText { get; private set; } = string.Empty;

    // 專案名稱 -> OAuth ClientId
    public Dictionary<string, string> ClientIds { get; } = [];

    // 專案名稱 -> 專案 Id，讓不同步驟類別都能以名稱引用情境中建立的專案。
    public Dictionary<string, Guid> ApplicationIds { get; } = [];

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

    public async Task SendAsync(HttpMethod method, string url, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        this.LastResponse = await this.Client.SendAsync(request);
        this.LastBodyText = await this.LastResponse.Content.ReadAsStringAsync();
        this.LastBody = this.LastBodyText.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(this.LastBodyText);
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
