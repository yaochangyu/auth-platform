using MemberApi.Contracts;

namespace MemberApi.Handlers;

public interface ILoginHandler
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
