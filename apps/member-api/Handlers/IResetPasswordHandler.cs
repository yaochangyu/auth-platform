using MemberApi.Contracts;

namespace MemberApi.Handlers;

public interface IResetPasswordHandler
{
    Task<ResetPasswordOutcome> HandleAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}
