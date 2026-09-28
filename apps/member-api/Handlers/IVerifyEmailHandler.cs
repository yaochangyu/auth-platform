using MemberApi.Contracts;

namespace MemberApi.Handlers;

public interface IVerifyEmailHandler
{
    Task<VerifyEmailResult> VerifyAsync(VerifyEmailRequest request, CancellationToken cancellationToken);
}
