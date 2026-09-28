using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Repositories;
using MemberApi.Security;

namespace MemberApi.Handlers;

public class VerifyEmailHandler(IMemberRepository memberRepository, TimeProvider timeProvider) : IVerifyEmailHandler
{
    public async Task<VerifyEmailResult> VerifyAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = VerificationTokenHasher.Hash(request.VerificationToken);
        var token = await memberRepository.FindVerificationTokenByHashAsync(tokenHash, cancellationToken);
        if (token is null)
        {
            return new VerifyEmailResult(VerifyEmailOutcome.TokenNotFound, null);
        }

        var now = timeProvider.GetUtcNow();
        if (token.UsedAt is not null || token.ExpiresAt < now)
        {
            return new VerifyEmailResult(VerifyEmailOutcome.TokenExpiredOrUsed, null);
        }

        token.UsedAt = now;

        var member = token.Member!;
        member.Status = MemberStatus.Active;

        await memberRepository.SaveChangesAsync(cancellationToken);

        var response = new VerifyEmailResponse(
            member.Id,
            member.Email,
            MemberStatus.Active,
            "信箱驗證成功，會員身分已啟用，現在可登入使用。");

        return new VerifyEmailResult(VerifyEmailOutcome.Verified, response);
    }
}
