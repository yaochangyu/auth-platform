using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Repositories;
using MemberApi.Security;
using Microsoft.AspNetCore.Identity;

namespace MemberApi.Handlers;

public class ResetPasswordHandler(IMemberRepository memberRepository, IPasswordHasher<Member> passwordHasher, TimeProvider timeProvider) : IResetPasswordHandler
{
    public async Task<ResetPasswordOutcome> HandleAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = VerificationTokenHasher.Hash(request.VerificationToken);
        var token = await memberRepository.FindVerificationTokenByHashAsync(tokenHash, cancellationToken);
        if (token is null || token.Purpose != VerificationTokenPurpose.PasswordReset)
        {
            return ResetPasswordOutcome.TokenNotFound;
        }

        var now = timeProvider.GetUtcNow();
        if (token.UsedAt is not null || token.ExpiresAt < now)
        {
            return ResetPasswordOutcome.TokenExpiredOrUsed;
        }

        var member = token.Member!;

        token.UsedAt = now;
        member.PasswordHash = passwordHasher.HashPassword(member, request.NewPassword);

        // ADR-0002：刷新安全戳記，Cookie 驗證中介層比對此值時會發現不一致，使所有歷史裝置的 Session 立即失效
        member.SecurityStamp = Guid.NewGuid().ToString("N");

        await memberRepository.SaveChangesAsync(cancellationToken);

        return ResetPasswordOutcome.Success;
    }
}
