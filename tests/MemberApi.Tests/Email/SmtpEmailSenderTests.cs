using MailKit.Security;
using MemberApi.Email;
using MemberApi.Tests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MemberApi.Tests.Email;

public class SmtpEmailSenderTests
{
    [Fact]
    public void SmtpOptions_ShouldHaveSensibleDefaults()
    {
        var options = new SmtpOptions();

        Assert.Equal("mailpit", options.Host);
        Assert.Equal(1025, options.Port);
        Assert.Equal("no-reply@1111.com.tw", options.FromEmail);
        Assert.Equal("1111 人力銀行", options.FromName);
        Assert.Equal(SecureSocketOptions.Auto, options.Security);
        Assert.Null(options.UserName);
        Assert.Null(options.Password);
    }

    [Fact]
    public void EmailProvider_WhenConfiguredAsLogging_ResolvesLoggingEmailSender()
    {
        using var factory = new MemberApiWebApplicationFactory(TestRunHooks.ConnectionString);
        var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        Assert.IsType<LoggingEmailSender>(emailSender);
    }

    [Fact]
    public void EmailProvider_WhenConfiguredAsSmtp_ResolvesSmtpEmailSender()
    {
        using var factory = new MemberApiWebApplicationFactory(TestRunHooks.ConnectionString);
        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:Provider"] = "Smtp"
                });
            });
        });

        var client = customFactory.CreateClient();

        using var scope = customFactory.Services.CreateScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        Assert.IsType<SmtpEmailSender>(emailSender);
    }
}
