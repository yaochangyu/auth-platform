using MemberApi.Contracts;

namespace MemberApi.Handlers;

public enum SendSmsOtpOutcome
{
    Success,
    RateLimited,
    Failed,
}

public record SendSmsOtpResult(SendSmsOtpOutcome Outcome, string? Message = null, int RetryAfterSeconds = 60);

public interface ISendSmsOtpHandler
{
    Task<SendSmsOtpResult> HandleAsync(SendSmsOtpRequest request, CancellationToken cancellationToken);
}
