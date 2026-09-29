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
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ConsentTicketService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".AspNetCore.Cookies"; // 須與 member-api 簽發的 Session Cookie 名稱一致
        options.Events.OnValidatePrincipal = async context =>
        {
            // ADR-0002：Security Stamp 與資料庫不符（密碼已變更/重設）即視為未登入；缺少 claim 一律 fail-closed。
            var memberIdText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = context.Principal?.FindFirstValue(MemberStamp.ClaimType);
            if (!Guid.TryParse(memberIdText, out var memberId) || stamp is null)
            {
                context.RejectPrincipal();
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AuthServerDbContext>();
            var currentStamp = await MemberStamp.GetCurrentAsync(dbContext, memberId, context.HttpContext.RequestAborted);
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
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<AuthServerDbContext>();
        options.ReplaceApplicationManager(typeof(MultiSecretApplicationManager<>));

        // Client 設定與 Secret 由 developer-api 在另一個行程寫入；快取只會在本行程內失效，
        // 開著會讓輪替、作廢的結果延遲生效，所以停用（每次驗證直接讀資料庫）。
        options.DisableEntityCaching();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetUserInfoEndpointUris("/connect/userinfo")
            .SetConfigurationEndpointUris("/.well-known/openid-configuration")
            .SetJsonWebKeySetEndpointUris("/.well-known/jwks.json");

        // 固定 issuer，避免經 Proxy/容器時由 Host header 推導而漂移；未設定才依請求推導（本機/測試）。
        if (builder.Configuration["Auth:Issuer"] is { Length: > 0 } issuer)
        {
            options.SetIssuer(new Uri(issuer));
        }

        options.AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .RequireProofKeyForCodeExchange()
            .RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess, AuthScopes.DeveloperApi)
            .SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(1))
            .SetAccessTokenLifetime(TimeSpan.FromMinutes(15))
            .SetRefreshTokenLifetime(TimeSpan.FromDays(30))
            // 預設有 30 秒寬限：舊 Refresh Token 仍可重複使用而不被視為外洩。ADR 0006 要求一律偵測，故歸零。
            .SetRefreshTokenReuseLeeway(TimeSpan.Zero);
        options.Configure(server =>
        {
            server.CodeChallengeMethods.Clear();
            server.CodeChallengeMethods.Add(CodeChallengeMethods.Sha256);
        });
        options.AddSigningKey(KeyStore.LoadOrCreateSigningKey(keyDirectory))
            .AddEncryptionKey(KeyStore.LoadOrCreateEncryptionKey(keyDirectory))
            .DisableAccessTokenEncryption();

        var aspNetCore = options.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough().EnableUserInfoEndpointPassthrough();
        if (!requireHttps)
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AuthServerDbContext>().Database.MigrateAsync();
    await ClientSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>(), builder.Configuration["Auth:DemoClientSecret"]);
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
