using MemberApi.Sms;
using MemberApi.Tests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MemberApi.Tests.Sms;

public class MitakeSmsSenderTests
{
    [Fact]
    public void MitakeSmsOptions_ShouldHaveSensibleDefaults()
    {
        var options = new MitakeSmsOptions();

        Assert.Equal("http://smspit:8026/api/mtk/SmSend", options.EndpointUrl);
        Assert.Equal("", options.Username);
        Assert.Equal("", options.Password);
        Assert.Equal(10, options.TimeoutSeconds);
    }

    [Fact]
    public void SmsProvider_WhenConfiguredAsLogging_ResolvesLoggingSmsSender()
    {
        using var factory = new MemberApiWebApplicationFactory(TestRunHooks.ConnectionString);
        var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var smsSender = scope.ServiceProvider.GetRequiredService<ISmsSender>();

        Assert.IsType<LoggingSmsSender>(smsSender);
    }

    [Fact]
    public void SmsProvider_WhenConfiguredAsMitake_ResolvesMitakeSmsSender()
    {
        using var factory = new MemberApiWebApplicationFactory(TestRunHooks.ConnectionString);
        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Sms:Provider"] = "Mitake",
                    ["MitakeSms:EndpointUrl"] = "http://localhost:8026/api/mtk/SmSend"
                });
            });
        });

        var client = customFactory.CreateClient();

        using var scope = customFactory.Services.CreateScope();
        var smsSender = scope.ServiceProvider.GetRequiredService<ISmsSender>();

        Assert.IsType<MitakeSmsSender>(smsSender);
    }
}
