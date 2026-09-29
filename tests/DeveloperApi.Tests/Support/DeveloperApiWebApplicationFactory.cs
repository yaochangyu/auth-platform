using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace DeveloperApi.Tests.Support;

public class DeveloperApiWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");
        builder.UseSetting("ConnectionStrings:DeveloperApiDb", connectionString);

        // 以測試金鑰取代 Authority 的 OIDC Discovery / JWKS 下載，其餘驗證規則（簽章、issuer、過期）沿用正式設定。
        builder.ConfigureTestServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var configuration = new OpenIdConnectConfiguration { Issuer = TestJwtIssuer.Issuer };
            configuration.SigningKeys.Add(TestJwtIssuer.SigningKey);
            options.Authority = null;
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        }));
    }
}
