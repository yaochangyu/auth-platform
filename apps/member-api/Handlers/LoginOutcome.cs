using MemberApi.Contracts;
using MemberApi.Entities;

namespace MemberApi.Handlers;

public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    MemberPending,
    MemberSuspended,
}

public record LoginResult(LoginOutcome Outcome, LoginResponse? Response, Member? Member);
