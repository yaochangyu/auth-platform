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

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".AspNetCore.Cookies";
        options.Cookie.Domain = ".1111.com.tw";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
            var memberIdText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stampInCookie = context.Principal?.FindFirstValue(SecurityStampClaimTypes.ClaimType);

            if (memberIdText is null || !Guid.TryParse(memberIdText, out var memberId))
            {
                context.RejectPrincipal();
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<MemberApiDbContext>();
            var currentStamp = await dbContext.Members
                .Where(member => member.Id == memberId)
                .Select(member => member.SecurityStamp)
                .SingleOrDefaultAsync();

            if (currentStamp is null || currentStamp != stampInCookie)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddDbContext<MemberApiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? builder.Configuration.GetConnectionString("MemberApiDb"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IHealthRepository, HealthRepository>();
builder.Services.AddScoped<IHealthCheckHandler, HealthCheckHandler>();
builder.Services.AddScoped<IMemberRepository, MemberRepository>();
builder.Services.AddScoped<IRegisterMemberHandler, RegisterMemberHandler>();
builder.Services.AddScoped<IVerifyEmailHandler, VerifyEmailHandler>();
builder.Services.AddScoped<ILoginHandler, LoginHandler>();
builder.Services.AddScoped<IForgotPasswordHandler, ForgotPasswordHandler>();
builder.Services.AddScoped<IResetPasswordHandler, ResetPasswordHandler>();
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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}
