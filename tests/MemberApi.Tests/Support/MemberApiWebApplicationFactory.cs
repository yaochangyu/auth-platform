using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using Microsoft.AspNetCore.TestHost;

namespace MemberApi.Tests.Support;

public class MemberApiWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    // 讓 BDD 情境可直接推進時間，驗證 15 分鐘鎖定自動解除等時效邏輯，不需要真的等待。
    public FakeTimeProvider TimeProvider { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ponytail: 停用 appsettings 熱重載監看，測試環境不需要，且可避免大量情境併發啟動時耗盡 inotify watcher 上限
        builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MemberApiDb"] = connectionString,
                ["Email:Provider"] = "Logging",
                ["Sms:Provider"] = "Logging",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(this.TimeProvider);
            services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    var configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
                    {
                        Issuer = TestJwtIssuer.Issuer,
                    };
                    configuration.SigningKeys.Add(TestJwtIssuer.SigningKey);
                    options.Authority = null;
                    options.RequireHttpsMetadata = false;
                    options.ConfigurationManager = new Microsoft.IdentityModel.Protocols.StaticConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>(configuration);
                });
        });
    }
}
