using System.Text.Json.Serialization;
using FluentValidation;
using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Handlers;
using MemberApi.Infrastructure.Persistence;
using MemberApi.Repositories;
using MemberApi.Validators;
using MemberApi.Workers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MemberApi.Email;
using MemberApi.Security;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<VerifyEmailRequest>, VerifyEmailRequestValidator>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
builder.Services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
builder.Services.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();

// ponytail: 容器化本機驗證環境沒有 TLS 終止，Secure Cookie 在純 HTTP 下無法寫入；
// 以設定檔控制而非寫死，正式環境維持預設安全值，docker-compose 才需要放寬。
var requireHttps = builder.Configuration.GetValue("Auth:RequireHttps", true);
var cookieDomain = builder.Configuration["Auth:CookieDomain"] ?? ".1111.com.tw";

// 與 auth-server 共用 Application Name 與金鑰目錄，auth-server 才能解開此處簽發的 Session Cookie 實現 SSO。
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("auth-platform");
if (builder.Configuration["Auth:DataProtectionKeyDirectory"] is { Length: > 0 } dataProtectionKeyDirectory)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyDirectory));
}

const string DualScheme = "DualScheme";

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = DualScheme;
    options.DefaultAuthenticateScheme = DualScheme;
    options.DefaultChallengeScheme = DualScheme;
})
.AddPolicyScheme(DualScheme, "Cookie or Bearer", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }
        return CookieAuthenticationDefaults.AuthenticationScheme;
    };
})
.AddCookie(options =>
{
    options.Cookie.Name = ".AspNetCore.Cookies";
    options.Cookie.Domain = cookieDomain;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Events.OnRedirectToLogin = async context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://auth.1111.com.tw/errors/unauthorized",
                Title = "未提供有效 Session Cookie 或會話已逾期失效",
                Status = StatusCodes.Status401Unauthorized,
            },
        });
    };
    options.Events.OnRedirectToAccessDenied = async context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://auth.1111.com.tw/errors/forbidden",
                Title = "目前身分不允許存取此資源",
                Status = StatusCodes.Status403Forbidden,
            },
        });
    };
    options.Events.OnValidatePrincipal = async context =>
    {
        // ADR-0002：每次驗證請求時比對 Cookie 中攜帶的安全戳記與資料庫最新值，
        // 密碼變更/重設後戳記已刷新，不一致即代表此 Session 已被使用者本人或系統主動註銷。
        //
        // 刻意不加 Cache：ADR-0002 的決策是「立即失效」，任何 TTL 快取都會重新製造一段
        // 「密碼已重設但舊 Cookie 仍可用」的視窗，直接違背這條 ADR 存在的目的。這裡查的是
        // members 表以主鍵 Id 查詢的單欄位索引掃描，屬於次毫秒等級的開銷，且只在已通過
        // Cookie 簽章驗證、確實帶有效身分的請求上執行，不是隨意可觸發的放大攻擊面；除非之後
        // 有實測數據證明它是瓶頸，否則不值得用快取換取安全性下降。
        //
        // 缺少 claim（例如遠早於此機制上線、格式較舊的殘留 Cookie）一律視為失效並要求重新登入，
        // 這是刻意的 fail-closed 設計，不是需要相容處理的缺陷：沒有戳記代表無法驗證這個 Session
        // 是否仍然有效，放行反而等於製造一條繞過安全戳記檢查的後門。
        var memberIdText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stampInCookie = context.Principal?.FindFirstValue(SecurityStampClaimTypes.ClaimType);

        if (memberIdText is null || stampInCookie is null || !Guid.TryParse(memberIdText, out var memberId))
        {
            context.RejectPrincipal();
            return;
        }

        var dbContext = context.HttpContext.RequestServices.GetRequiredService<MemberApiDbContext>();
        var currentStamp = await dbContext.Members
            .Where(member => member.Id == memberId)
            .Select(member => member.SecurityStamp)
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (currentStamp is null || currentStamp != stampInCookie)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    };
})
.AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Auth:Authority"];
    options.RequireHttpsMetadata = builder.Configuration.GetValue("Auth:RequireHttps", true);
    options.MapInboundClaims = false;
    options.TokenValidationParameters.ValidateAudience = false;
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context.HttpContext,
                ProblemDetails = new ProblemDetails
                {
                    Type = "https://auth.1111.com.tw/errors/unauthorized",
                    Title = "未授權：未提供有效 Bearer Token 或 Token 已過期失效",
                    Status = StatusCodes.Status401Unauthorized,
                },
            });
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context.HttpContext,
                ProblemDetails = new ProblemDetails
                {
                    Type = "https://auth.1111.com.tw/errors/forbidden",
                    Title = "權限不足：目前 Token 不允許存取此資源",
                    Status = StatusCodes.Status403Forbidden,
                },
            });
        },
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ProfileAccess", policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context =>
        {
            // Cookie 鑑權（第一方瀏覽器會話）：擁有完整個人檔案存取權限
            if (context.User.Identity?.AuthenticationType == CookieAuthenticationDefaults.AuthenticationScheme)
            {
                return true;
            }

            // Bearer Token 鑑權：必須具備 profile scope
            var scopes = context.User.FindAll("scope")
                .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return scopes.Contains("profile");
        }));
});

builder.Services.AddDbContext<MemberApiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? builder.Configuration.GetConnectionString("MemberApiDb"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IHealthRepository, HealthRepository>();
builder.Services.AddScoped<IHealthCheckHandler, HealthCheckHandler>();
builder.Services.AddScoped<IMemberRepository, MemberRepository>();
builder.Services.AddScoped<IConnectedAppRepository, ConnectedAppRepository>();
builder.Services.AddScoped<IRegisterMemberHandler, RegisterMemberHandler>();
builder.Services.AddScoped<IVerifyEmailHandler, VerifyEmailHandler>();
builder.Services.AddScoped<ILoginHandler, LoginHandler>();
builder.Services.AddScoped<IForgotPasswordHandler, ForgotPasswordHandler>();
builder.Services.AddScoped<IResetPasswordHandler, ResetPasswordHandler>();
builder.Services.AddScoped<IGetMemberProfileHandler, GetMemberProfileHandler>();
builder.Services.AddScoped<IChangePasswordHandler, ChangePasswordHandler>();
builder.Services.AddScoped<IListConnectedAppsHandler, ListConnectedAppsHandler>();
builder.Services.AddScoped<IRevokeConnectedAppHandler, RevokeConnectedAppHandler>();
builder.Services.AddScoped<IPasswordHasher<Member>, PasswordHasher<Member>>();
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
builder.Services.AddHostedService<EmailDispatchWorker>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

if (requireHttps)
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<MemberApiDbContext>().Database.MigrateAsync();
}

app.Run();

public partial class Program
{
}
