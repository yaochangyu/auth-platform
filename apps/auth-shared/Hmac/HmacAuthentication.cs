using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthShared.Hmac;

/// <summary>由 API Key 找出用來驗簽的 API Secret 與此金鑰對應的身分 claims；金鑰不存在、已過期或已作廢時回傳 null。</summary>
public record HmacCredential(string Secret, IReadOnlyList<Claim> Claims);

public interface IHmacCredentialResolver
{
    Task<HmacCredential?> ResolveAsync(string apiKey, CancellationToken cancellationToken);
}

public class HmacAuthenticationOptions : AuthenticationSchemeOptions
{
    // 時間戳記與伺服器時間的最大差距（防重放）。
    public TimeSpan MaxClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    public long MaxBodyBytes { get; set; } = 1024 * 1024;
}

public static class HmacAuthenticationDefaults
{
    public const string Scheme = "HmacSignature";
}

/// <summary>
/// 驗證 X-Api-Key、X-Timestamp、X-Signature：時間戳記需在 ±MaxClockSkew 內，簽章需與 Method、Path+Query、Timestamp、Body 相符。
/// 失敗一律回 401，不對外說明是哪一項不符（避免給攻擊者線索）；原因只寫入除錯日誌。
/// </summary>
public class HmacAuthenticationHandler(
    IOptionsMonitor<HmacAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TimeProvider timeProvider,
    IHmacCredentialResolver resolver) : AuthenticationHandler<HmacAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = this.Request.Headers;
        if (!headers.TryGetValue(HmacSignature.ApiKeyHeader, out var apiKey) || !headers.TryGetValue(HmacSignature.TimestampHeader, out var timestampText)
            || !headers.TryGetValue(HmacSignature.SignatureHeader, out var signature))
        {
            return AuthenticateResult.NoResult();
        }

        // 以純數值比較，避免極端的時間戳記讓 DateTimeOffset 轉換丟出例外而變成 500。
        if (!long.TryParse(timestampText, out var timestamp)
            || Math.Abs(timeProvider.GetUtcNow().ToUnixTimeSeconds() - timestamp) > this.Options.MaxClockSkew.TotalSeconds)
        {
            return this.Fail("時間戳記無效或超出允許範圍");
        }

        if (this.Request.ContentLength > this.Options.MaxBodyBytes)
        {
            return this.Fail("Body 過大");
        }

        var credential = await resolver.ResolveAsync(apiKey.ToString(), this.Context.RequestAborted);
        if (credential is null)
        {
            return this.Fail("API Key 不存在、已過期或已作廢");
        }

        // 讀完 Body 後要倒回起點，後面的 Model Binding 才讀得到。分塊傳輸沒有 Content-Length，
        // 所以在緩衝時就限制大小，超過即拒絕（驗簽之前不能讓未認證的請求吃掉大量記憶體）。
        this.Request.EnableBuffering(bufferThreshold: 64 * 1024, bufferLimit: this.Options.MaxBodyBytes);
        using var buffer = new MemoryStream();
        try
        {
            await this.Request.Body.CopyToAsync(buffer, this.Context.RequestAborted);
        }
        catch (InvalidDataException)
        {
            return this.Fail("Body 過大");
        }

        this.Request.Body.Position = 0;

        var pathAndQuery = this.Request.PathBase + this.Request.Path + this.Request.QueryString;
        if (!HmacSignature.Verify(credential.Secret, signature.ToString(), this.Request.Method, pathAndQuery, timestamp, buffer.ToArray()))
        {
            return this.Fail("簽章不符");
        }

        var identity = new ClaimsIdentity(credential.Claims, this.Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), this.Scheme.Name));
    }

    private AuthenticateResult Fail(string reason)
    {
        this.Logger.LogDebug("HMAC 驗證失敗：{Reason}", reason);
        return AuthenticateResult.Fail(reason);
    }
}

public static class HmacAuthenticationExtensions
{
    public static AuthenticationBuilder AddHmacSignature<TResolver>(this AuthenticationBuilder builder)
        where TResolver : class, IHmacCredentialResolver
    {
        builder.Services.AddScoped<IHmacCredentialResolver, TResolver>();
        return builder.AddScheme<HmacAuthenticationOptions, HmacAuthenticationHandler>(HmacAuthenticationDefaults.Scheme, _ => { });
    }
}
