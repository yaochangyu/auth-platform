using MemberApi.Contracts;

namespace MemberApi.Handlers;

public interface IForgotPasswordHandler
{
    Task<ForgotPasswordOutcome> HandleAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);
}
