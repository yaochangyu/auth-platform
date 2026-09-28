using MemberApi.Contracts;

namespace MemberApi.Handlers;

public enum VerifyEmailOutcome
{
    Verified,
    TokenNotFound,
    TokenExpiredOrUsed,
    MemberNotPending,
}

public record VerifyEmailResult(VerifyEmailOutcome Outcome, VerifyEmailResponse? Response);
