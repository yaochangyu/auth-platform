using MemberApi.Contracts;

namespace MemberApi.Handlers;

public enum VerifyPhoneOutcome
{
    Success,
    InvalidCode,
    Expired,
    MaxAttemptsReached,
    PhoneAlreadyBound,
    MemberNotFound,
}

public record VerifyPhoneResult(
    VerifyPhoneOutcome Outcome,
    string? Message = null,
    DateTimeOffset? VerifiedAt = null);

public interface IVerifyPhoneHandler
{
    Task<VerifyPhoneResult> HandleAsync(VerifyPhoneRequest request, Guid? currentMemberId, CancellationToken cancellationToken);
}
