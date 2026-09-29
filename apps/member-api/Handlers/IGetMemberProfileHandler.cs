using MemberApi.Contracts;

namespace MemberApi.Handlers;

public interface IGetMemberProfileHandler
{
    Task<MemberProfileResponse?> HandleAsync(Guid memberId, CancellationToken cancellationToken);
}
