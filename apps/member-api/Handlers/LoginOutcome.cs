using MemberApi.Contracts;
using MemberApi.Entities;

namespace MemberApi.Handlers;

public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    MemberPending,
    MemberSuspended,
    AccountLocked,
}

public record LoginResult(
    LoginOutcome Outcome,
    LoginResponse? Response,
    Member? Member,
    int? FailedLoginAttempts = null,
    DateTimeOffset? LockoutEndAt = null);
