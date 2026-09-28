using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Repositories;
using Microsoft.AspNetCore.Identity;

namespace MemberApi.Handlers;

public class LoginHandler(IMemberRepository memberRepository, IPasswordHasher<Member> passwordHasher) : ILoginHandler
{
    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var member = await memberRepository.FindByEmailAsync(request.Email, cancellationToken);
        if (member is null || passwordHasher.VerifyHashedPassword(member, member.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return new LoginResult(LoginOutcome.InvalidCredentials, null, null);
        }

        if (member.Status == MemberStatus.Pending)
        {
            return new LoginResult(LoginOutcome.MemberPending, null, member);
        }

        if (member.Status == MemberStatus.Suspended)
        {
            return new LoginResult(LoginOutcome.MemberSuspended, null, member);
        }

        var response = new LoginResponse(member.Id, member.Email, member.DisplayName, member.Status, request.ReturnUrl);
        return new LoginResult(LoginOutcome.Success, response, member);
    }
}
