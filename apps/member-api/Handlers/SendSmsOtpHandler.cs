using MemberApi.Contracts;
using MemberApi.Infrastructure.Persistence;
using MemberApi.Sms;
using Microsoft.EntityFrameworkCore;

namespace MemberApi.Handlers;

public enum SendSmsOtpOutcome
{
    Success,
    RateLimited,
    Failed,
}

public record SendSmsOtpResult(SendSmsOtpOutcome Outcome, string? Message = null, int RetryAfterSeconds = 60);

public class SendSmsOtpHandler(
    ISmsOtpService smsOtpService,
    ISmsSender smsSender,
    MemberApiDbContext dbContext,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(60);

    public async Task<SendSmsOtpResult> HandleAsync(SendSmsOtpRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // 查詢該手機與該用途最新一筆 OTP 是否仍在 60 秒冷卻期內
        var latestOtp = await dbContext.SmsOtps
            .Where(x => x.PhoneNumber == request.PhoneNumber && x.Purpose == request.Purpose)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestOtp is not null && now - latestOtp.CreatedAt < CooldownDuration)
        {
            var remainingSeconds = (int)Math.Ceiling((CooldownDuration - (now - latestOtp.CreatedAt)).TotalSeconds);
            return new SendSmsOtpResult(SendSmsOtpOutcome.RateLimited, RetryAfterSeconds: Math.Max(1, remainingSeconds));
        }

        var otpResult = await smsOtpService.GenerateOtpAsync(request.PhoneNumber, request.Purpose, cancellationToken);

        var smsMessage = $"【1111平台】您的驗證碼為 {otpResult.Code}，有效時間 5 分鐘，請勿提供他人。";
        try
        {
            await smsSender.SendAsync(request.PhoneNumber, smsMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            return new SendSmsOtpResult(SendSmsOtpOutcome.Failed, ex.Message);
        }

        return new SendSmsOtpResult(SendSmsOtpOutcome.Success, "驗證碼已發送至指定手機號碼，請於 5 分鐘內完成驗證。", (int)CooldownDuration.TotalSeconds);
    }
}
