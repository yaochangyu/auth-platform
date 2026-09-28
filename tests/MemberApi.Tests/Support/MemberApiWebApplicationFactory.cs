using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MemberApi.Tests.Support;

public class MemberApiWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ponytail: 停用 appsettings 熱重載監看，測試環境不需要，且可避免大量情境併發啟動時耗盡 inotify watcher 上限
        builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MemberApiDb"] = connectionString,
            });
        });
    }
}
