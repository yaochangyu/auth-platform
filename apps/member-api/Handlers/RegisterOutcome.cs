using MemberApi.Contracts;

namespace MemberApi.Handlers;

public enum RegisterOutcome
{
    Created,
    EmailAlreadyActive,
    PhoneAlreadyBound,
}

public record RegisterResult(RegisterOutcome Outcome, RegisterResponse? Response);
