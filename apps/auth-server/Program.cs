using AuthServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var keyDirectory = builder.Configuration["Auth:KeyDirectory"] ?? ".";
var requireHttps = builder.Configuration.GetValue("Auth:RequireHttps", true);

builder.Services.AddDbContext<AuthServerDbContext>(options =>
{
    // ADR 0006：與 MemberApi 共用資料庫，migration 記錄表必須獨立，否則兩邊歷史互相覆蓋。
    options.UseNpgsql(
            builder.Configuration.GetConnectionString("AuthServerDb"),
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history_auth_server"))
        .UseSnakeCaseNamingConvention();
    options.UseOpenIddict();
});

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AuthServerDbContext>())
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetConfigurationEndpointUris("/.well-known/openid-configuration")
            .SetJsonWebKeySetEndpointUris("/.well-known/jwks.json");

        // 固定 issuer，避免經 Proxy/容器時由 Host header 推導而漂移；未設定才依請求推導（本機/測試）。
        if (builder.Configuration["Auth:Issuer"] is { Length: > 0 } issuer)
        {
            options.SetIssuer(new Uri(issuer));
        }

        options.AllowAuthorizationCodeFlow();
        options.AddSigningKey(KeyStore.LoadOrCreateSigningKey(keyDirectory))
            .AddEncryptionKey(KeyStore.LoadOrCreateEncryptionKey(keyDirectory))
            .DisableAccessTokenEncryption();

        var aspNetCore = options.UseAspNetCore();
        if (!requireHttps)
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AuthServerDbContext>().Database.MigrateAsync();
}

app.Run();

public partial class Program
{
}
