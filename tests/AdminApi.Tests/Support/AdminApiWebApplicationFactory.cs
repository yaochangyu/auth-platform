using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace AdminApi.Tests.Support;

public class AdminApiWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string ClientIp = "203.0.113.9";

    public FakeTimeProvider TimeProvider { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");
        builder.UseSetting("ConnectionStrings:AdminApiDb", connectionString);

        // 測試中的連線來源（203.0.113.9）視為受信任的反向代理。
        builder.UseSetting("Auth:TrustedProxies:0", ClientIp);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(this.TimeProvider);
            services.AddTransient<IStartupFilter, FixedClientIpStartupFilter>();

            // 以測試金鑰取代 Authority 的 OIDC Discovery / JWKS 下載，其餘驗證規則（簽章、issuer、過期）沿用正式設定。
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = TestJwtIssuer.Issuer };
                configuration.SigningKeys.Add(TestJwtIssuer.SigningKey);
                options.Authority = null;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }

    // TestServer 沒有真實的連線來源，固定一個位址以驗證稽核紀錄有記下客戶端 IP。
    private sealed class FixedClientIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(ClientIp);
                return nextMiddleware();
            });
            next(app);
        };
    }
}
