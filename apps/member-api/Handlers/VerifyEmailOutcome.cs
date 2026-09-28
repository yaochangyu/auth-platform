using MemberApi.Contracts;

namespace MemberApi.Handlers;

public enum VerifyEmailOutcome
{
    Verified,
    TokenNotFound,
    TokenExpiredOrUsed,
}

public record VerifyEmailResult(VerifyEmailOutcome Outcome, VerifyEmailResponse? Response);
