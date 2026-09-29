using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace AuthServer.Tests.Support;

public class AuthServerWebApplicationFactory(string connectionString, string keyDirectory) : WebApplicationFactory<Program>
{
    // 讓 BDD 情境可直接推進時間驗證 Consent Ticket 5 分鐘過期，不需要真的等待。
    public FakeTimeProvider TimeProvider { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");
        builder.UseSetting("ConnectionStrings:AuthServerDb", connectionString);
        builder.UseSetting("Auth:KeyDirectory", keyDirectory);
        builder.UseSetting("Auth:RequireHttps", "false");
        builder.UseSetting("Auth:DemoClientSecret", "demo-secret-for-tests");
        builder.UseSetting("Auth:MemberLoginUrl", "https://member.1111.com.tw/login");
        builder.UseSetting("Auth:MemberConsentUrl", "https://member.1111.com.tw/oauth/consent");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(this.TimeProvider);
        });
    }
}
