using MemberApi.Contracts;

namespace MemberApi.Handlers;

public interface IRegisterMemberHandler
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
}
