using System.Security.Claims;
using System.Text.Json.Serialization;
using DeveloperApi;
using DeveloperApi.Contracts;
using DeveloperApi.Infrastructure;
using DeveloperApi.Repositories;
using DeveloperApi.Security;
using Microsoft.AspNetCore.DataProtection;
using DeveloperApi.Validators;
using DeveloperApi.Security.Hmac;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IValidator<ApplicationRequest>, ApplicationRequestValidator>();
builder.Services.AddScoped<IValidator<OAuthClientRequest>, OAuthClientRequestValidator>();
builder.Services.AddScoped<ApplicationRepository>();
builder.Services.AddScoped<OAuthClientRepository>();
builder.Services.AddScoped<ApiKeyRepository>();
builder.Services.AddScoped<IValidator<ApiKeyRequest>, ApiKeyRequestValidator>();

// API Secret 以 Data Protection 加密保存；Application Name 與金鑰目錄須固定，重啟或多實例才解得開。
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("auth-platform");
if (builder.Configuration["Auth:DataProtectionKeyDirectory"] is { Length: > 0 } dataProtectionKeyDirectory)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyDirectory));
}

builder.Services.AddDbContext<DeveloperApiDbContext>(options =>
    options.UseNpgsql(
            builder.Configuration.GetConnectionString("DeveloperApiDb"),
            npgsql => npgsql.MigrationsHistoryTable(DeveloperApiDbContext.MigrationsHistoryTable))
        .UseSnakeCaseNamingConvention());

builder.Services.AddDbContext<OpenIddictStoreContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DeveloperApiDb")).UseSnakeCaseNamingConvention();
    options.UseOpenIddict();
});

// 只用 OpenIddict 的管理器讀寫 Client（auth-server 才是授權伺服器）。快取停用：auth-server 在另一個行程，
// 快取不會反映對方的異動，也避免這裡讀到過時的 Secret 集合。
builder.Services.AddOpenIddict().AddCore(options =>
{
    options.UseEntityFrameworkCore().UseDbContext<OpenIddictStoreContext>();
    options.DisableEntityCaching();
});

// Resource Server：以 auth-server 的 OIDC Discovery / JWKS 離線驗證 RS256 JWT（ADR 0005）。
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue("Auth:RequireHttps", true);
        options.MapInboundClaims = false;

        // ponytail: auth-server 目前不簽發 aud；改以 developer_api 範疇限定可用的 Token。日後有多個資源伺服器時改用 aud。
        options.TokenValidationParameters.ValidateAudience = false;
    })
    .AddHmacSignature<ApiKeyHmacResolver>();

builder.Services.AddAuthorization(options =>
{
    // M2M 端點需要 profile 範疇的 API Key。
    options.AddPolicy(AuthPolicies.M2mProfile, policy => policy
        .AddAuthenticationSchemes(HmacAuthenticationDefaults.Scheme)
        .RequireAuthenticatedUser()
        .RequireAssertion(context => context.User.FindFirstValue("scope")?.Split(' ').Contains("profile") == true));

    options.AddPolicy(AuthPolicies.DeveloperApi, policy => policy
    .RequireAuthenticatedUser()
    // sub 必須是有效的 MemberId（Guid），否則在 Controller 取用時會變成 500。
    .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue("sub"), out _)
        && context.User.FindFirstValue("scope")?.Split(' ').Contains(AuthPolicies.DeveloperApiScope) == true));
});

var app = builder.Build();

if (string.IsNullOrEmpty(builder.Configuration["Auth:DataProtectionKeyDirectory"]))
{
    // API Secret 以 Data Protection 加密保存；金鑰環未持久化時，重建容器後所有 API Key 都會無法驗證。
    app.Logger.LogWarning("未設定 Auth:DataProtectionKeyDirectory，重啟或重建後既有 API Key 的 API Secret 將無法解密。");
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DeveloperApiDbContext>().Database.MigrateAsync();
}

app.Run();

public partial class Program
{
}
