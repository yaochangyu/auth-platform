using MemberApi.Entities;

namespace MemberApi.Sms;

public record GenerateOtpResult(string Code, DateTimeOffset ExpiresAt);

public record VerifyOtpResult(bool Success, string? ErrorMessage, bool IsExpired = false, bool MaxAttemptsReached = false);

public interface ISmsOtpService
{
    Task<GenerateOtpResult> GenerateOtpAsync(string phoneNumber, SmsOtpPurpose purpose, CancellationToken cancellationToken = default);

    Task<VerifyOtpResult> VerifyOtpAsync(string phoneNumber, string code, SmsOtpPurpose purpose, CancellationToken cancellationToken = default);
}
