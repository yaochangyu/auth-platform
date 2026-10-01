using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Repositories;
using MemberApi.Security;

namespace MemberApi.Handlers;

public class ForgotPasswordHandler(IMemberRepository memberRepository, TimeProvider timeProvider)
{
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);

    public async Task<ForgotPasswordOutcome> HandleAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var member = await memberRepository.FindByEmailAsync(request.Email, cancellationToken);
        if (member is null)
        {
            // 隱私防護：查無會員時不做任何資料庫寫入，直接視為受理，回應內容與真實會員一致，防止帳號枚舉
            return ForgotPasswordOutcome.Accepted;
        }

        var now = timeProvider.GetUtcNow();

        var latestToken = await memberRepository.FindLatestVerificationTokenAsync(member.Id, VerificationTokenPurpose.PasswordReset, cancellationToken);
        if (latestToken is not null && now - latestToken.CreatedAt < CooldownDuration)
        {
            return ForgotPasswordOutcome.RateLimited;
        }

        // 每個會員同時僅維持單一有效重設密碼權杖，重新申請時作廢既有未使用權杖
        var activeTokens = await memberRepository.FindActiveVerificationTokensAsync(member.Id, VerificationTokenPurpose.PasswordReset, cancellationToken);
        foreach (var activeToken in activeTokens)
        {
            activeToken.UsedAt = now;
        }

        var rawToken = VerificationTokenGenerator.Generate();
        memberRepository.AddVerificationToken(new VerificationToken
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            TokenHash = VerificationTokenHasher.Hash(rawToken),
            Purpose = VerificationTokenPurpose.PasswordReset,
            ExpiresAt = now + TokenLifetime,
            CreatedAt = now,
        });

        memberRepository.AddOutboxMessage(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            ToEmail = member.Email,
            Subject = "重設您的密碼",
            Body = $"請點擊連結以重設您的密碼：https://auth.1111.com.tw/reset-password?token={rawToken}",
            CreatedAt = now,
        });

        await memberRepository.SaveChangesAsync(cancellationToken);

        return ForgotPasswordOutcome.Accepted;
    }
}
