using MemberApi.Entities;

namespace MemberApi.Handlers;

public enum ChangePasswordOutcome
{
    Success,
    InvalidCurrentPassword,
}

public record ChangePasswordResult(ChangePasswordOutcome Outcome, Member? Member);
