using MemberApi.Contracts;

namespace MemberApi.Handlers;

public record UpdateMemberProfileResult(
    UpdateMemberProfileOutcome Outcome,
    MemberProfileResponse? Profile = null);
