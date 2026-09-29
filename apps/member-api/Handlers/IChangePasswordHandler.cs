using MemberApi.Contracts;

namespace MemberApi.Handlers;

public interface IChangePasswordHandler
{
    Task<ChangePasswordResult> HandleAsync(Guid memberId, ChangePasswordRequest request, CancellationToken cancellationToken);
}
