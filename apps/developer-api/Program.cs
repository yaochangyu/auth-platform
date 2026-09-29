using System.Security.Claims;
using System.Text.Json.Serialization;
using DeveloperApi;
using DeveloperApi.Contracts;
using DeveloperApi.Infrastructure;
using DeveloperApi.Repositories;
using DeveloperApi.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IValidator<ApplicationRequest>, ApplicationRequestValidator>();
builder.Services.AddScoped<ApplicationRepository>();

builder.Services.AddDbContext<DeveloperApiDbContext>(options =>
    options.UseNpgsql(
            builder.Configuration.GetConnectionString("DeveloperApiDb"),
            npgsql => npgsql.MigrationsHistoryTable(DeveloperApiDbContext.MigrationsHistoryTable))
        .UseSnakeCaseNamingConvention());

// Resource Server：以 auth-server 的 OIDC Discovery / JWKS 離線驗證 RS256 JWT（ADR 0005）。
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue("Auth:RequireHttps", true);
        options.MapInboundClaims = false;

        // ponytail: auth-server 目前不簽發 aud；改以 developer_api 範疇限定可用的 Token。日後有多個資源伺服器時改用 aud。
        options.TokenValidationParameters.ValidateAudience = false;
    });

builder.Services.AddAuthorization(options => options.AddPolicy(AuthPolicies.DeveloperApi, policy => policy
    .RequireAuthenticatedUser()
    // sub 必須是有效的 MemberId（Guid），否則在 Controller 取用時會變成 500。
    .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue("sub"), out _)
        && context.User.FindFirstValue("scope")?.Split(' ').Contains(AuthPolicies.DeveloperApiScope) == true)));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DeveloperApiDbContext>().Database.MigrateAsync();
}

app.Run();

public partial class Program
{
}
