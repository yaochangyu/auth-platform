namespace AuthServer.Tests.Support;

public class AuthServerTestBase
{
    private AuthServerWebApplicationFactory? _factory;

    // 同一情境內重啟伺服器時沿用，才能驗證簽章金鑰確實落地而非每次重新產生。
    public string KeyDirectory { get; } = Directory.CreateTempSubdirectory("auth-server-keys-").FullName;

    public HttpClient Client { get; private set; } = null!;

    public HttpResponseMessage? LastResponse { get; set; }

    public void Start()
    {
        this._factory = new AuthServerWebApplicationFactory(TestRunHooks.ConnectionString, this.KeyDirectory);
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

    public void DeleteKeyDirectory() => Directory.Delete(this.KeyDirectory, recursive: true);
}
