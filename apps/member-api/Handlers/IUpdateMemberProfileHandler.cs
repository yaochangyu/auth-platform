using MemberApi.Contracts;

namespace MemberApi.Handlers;

public interface IUpdateMemberProfileHandler
{
    Task<UpdateMemberProfileResult> HandleAsync(
        Guid memberId,
        UpdateMemberProfileRequest request,
        CancellationToken cancellationToken);
}
