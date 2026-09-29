using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AuthServer.Tests.Support;

public class AuthServerWebApplicationFactory(string connectionString, string keyDirectory) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");
        builder.UseSetting("ConnectionStrings:AuthServerDb", connectionString);
        builder.UseSetting("Auth:KeyDirectory", keyDirectory);
        builder.UseSetting("Auth:RequireHttps", "false");
        builder.UseSetting("Auth:MemberLoginUrl", "https://member.1111.com.tw/login");
    }
}
