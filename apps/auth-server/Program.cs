using System.Security.Claims;
using AuthServer.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

var keyDirectory = builder.Configuration["Auth:KeyDirectory"] ?? ".";
var requireHttps = builder.Configuration.GetValue("Auth:RequireHttps", true);

// 與 member-api 共用 Application Name 與金鑰目錄，才能解開會員中心簽發的 .1111.com.tw Session Cookie。
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("auth-platform");
if (builder.Configuration["Auth:DataProtectionKeyDirectory"] is { Length: > 0 } dataProtectionKeyDirectory)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyDirectory));
}

builder.Services.AddControllers();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".AspNetCore.Cookies"; // 須與 member-api 簽發的 Session Cookie 名稱一致
        options.Events.OnValidatePrincipal = async context =>
        {
            // ADR-0002：Security Stamp 與資料庫不符（密碼已變更/重設）即視為未登入；缺少 claim 一律 fail-closed。
            // ponytail: 與 member-api 共用資料庫，直接讀 members.security_stamp（耦合其 schema）；改為內部 API 時再抽換。
            var memberIdText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = context.Principal?.FindFirstValue("security_stamp");
            if (!Guid.TryParse(memberIdText, out var memberId) || stamp is null)
            {
                context.RejectPrincipal();
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AuthServerDbContext>();
            var currentStamp = await dbContext.Database
                .SqlQuery<string>($"select security_stamp as \"Value\" from members where id = {memberId}")
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (currentStamp != stamp)
            {
                context.RejectPrincipal();
            }
        };
    });

builder.Services.AddDbContext<AuthServerDbContext>(options =>
{
    options.UseNpgsql(
            builder.Configuration.GetConnectionString("AuthServerDb"),
            npgsql => npgsql.MigrationsHistoryTable(AuthServerDbContext.MigrationsHistoryTable))
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

        options.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange()
            .RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email)
            .SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(1));
        options.Configure(server =>
        {
            server.CodeChallengeMethods.Clear();
            server.CodeChallengeMethods.Add(CodeChallengeMethods.Sha256);
        });
        options.AddSigningKey(KeyStore.LoadOrCreateSigningKey(keyDirectory))
            .AddEncryptionKey(KeyStore.LoadOrCreateEncryptionKey(keyDirectory))
            .DisableAccessTokenEncryption();

        var aspNetCore = options.UseAspNetCore().EnableAuthorizationEndpointPassthrough();
        if (!requireHttps)
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AuthServerDbContext>().Database.MigrateAsync();
    await ClientSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>());
}

if (string.IsNullOrEmpty(builder.Configuration["Auth:DataProtectionKeyDirectory"]))
{
    // 未共用金鑰目錄時，auth-server 解不開 member-api 簽發的 Cookie，所有請求都會被導向登入頁。
    app.Logger.LogWarning("未設定 Auth:DataProtectionKeyDirectory，將無法解開會員中心的 Session Cookie（SSO 失效）。");
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}
