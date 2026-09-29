using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using AdminApi;
using AdminApi.Contracts;
using AdminApi.Infrastructure;
using AdminApi.Services;
using AdminApi.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 稽核紀錄要記錄真正的客戶端 IP：只信任設定的反向代理（Auth:TrustedProxies）轉送的 X-Forwarded-For，
// 未設定時只信任本機，任意來源送來的轉送標頭一律忽略（否則呼叫端可以偽造自己的 IP）。
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("Auth:TrustedProxies").Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }
});

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IValidator<StatusChangeRequest>, StatusChangeRequestValidator>();
builder.Services.AddScoped<ApplicationStatusService>();

builder.Services.AddDbContext<AdminApiDbContext>(options =>
{
    options.UseNpgsql(
            builder.Configuration.GetConnectionString("AdminApiDb"),
            npgsql => npgsql.MigrationsHistoryTable(AdminApiDbContext.MigrationsHistoryTable))
        .UseSnakeCaseNamingConvention();
    options.UseOpenIddict();
});

// 只用 OpenIddict 的管理器撤銷授權與 Token、標記 Client 停用（auth-server 才是授權伺服器）。
// 快取停用：auth-server 在另一個行程，快取不會反映對方的異動。
builder.Services.AddOpenIddict().AddCore(options =>
{
    options.UseEntityFrameworkCore().UseDbContext<AdminApiDbContext>();
    options.DisableEntityCaching();
});

// Resource Server：以 auth-server 的 OIDC Discovery / JWKS 離線驗證 RS256 JWT（ADR 0005）。
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue("Auth:RequireHttps", true);
        options.MapInboundClaims = false;

        // ponytail: auth-server 目前不簽發 aud；改以 admin_api 範疇與 role 限定可用的 Token。
        options.TokenValidationParameters.ValidateAudience = false;
    });

// 僅限管理員：Token 需帶 admin_api 範疇（只授權給 admin-web），且 role 為 admin（auth-server 於換票當下讀取會員最新角色）。
builder.Services.AddAuthorization(options => options.AddPolicy(AuthPolicies.Admin, policy => policy
    .RequireAuthenticatedUser()
    .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue("sub"), out _)
        && context.User.FindFirstValue("scope")?.Split(' ').Contains(AuthPolicies.AdminApiScope) == true
        && context.User.FindFirstValue("role") == AuthPolicies.AdminRole)));

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AdminApiDbContext>().Database.MigrateAsync();
}

app.Run();

public partial class Program
{
}
