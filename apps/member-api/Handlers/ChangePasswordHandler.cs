using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Repositories;
using Microsoft.AspNetCore.Identity;

namespace MemberApi.Handlers;

public class ChangePasswordHandler(IMemberRepository memberRepository, IPasswordHasher<Member> passwordHasher) : IChangePasswordHandler
{
    public async Task<ChangePasswordResult> HandleAsync(Guid memberId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var member = await memberRepository.FindByIdAsync(memberId, cancellationToken);
        if (member is null || passwordHasher.VerifyHashedPassword(member, member.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            return new ChangePasswordResult(ChangePasswordOutcome.InvalidCurrentPassword, null);
        }

        member.PasswordHash = passwordHasher.HashPassword(member, request.NewPassword);

        // ADR-0002：刷新安全戳記使其他裝置的歷史 Session 立即失效；
        // 當前裝置由 Controller 在回應中以新戳記重新發行 Session Cookie，維持登入狀態。
        member.SecurityStamp = Guid.NewGuid().ToString("N");

        await memberRepository.SaveChangesAsync(cancellationToken);

        return new ChangePasswordResult(ChangePasswordOutcome.Success, member);
    }
}
