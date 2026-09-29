using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Repositories;
using Microsoft.AspNetCore.Identity;

namespace MemberApi.Handlers;

public class LoginHandler(IMemberRepository memberRepository, IPasswordHasher<Member> passwordHasher, TimeProvider timeProvider) : ILoginHandler
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var member = await memberRepository.FindByEmailAsync(request.Email, cancellationToken);
        if (member is null)
        {
            return new LoginResult(LoginOutcome.InvalidCredentials, null, null);
        }

        var now = timeProvider.GetUtcNow();

        // 鎖定期間內一律直接拒絕，即使密碼正確也不進行雜湊比對
        if (member.LockoutEndAt is not null && member.LockoutEndAt > now)
        {
            return new LoginResult(LoginOutcome.AccountLocked, null, member, member.FailedLoginAttempts, member.LockoutEndAt);
        }

        var passwordVerified = passwordHasher.VerifyHashedPassword(member, member.PasswordHash, request.Password) != PasswordVerificationResult.Failed;
        if (!passwordVerified)
        {
            member.FailedLoginAttempts++;

            if (member.FailedLoginAttempts >= MaxFailedAttempts)
            {
                member.LockoutEndAt = now + LockoutDuration;
                await memberRepository.SaveChangesAsync(cancellationToken);
                return new LoginResult(LoginOutcome.AccountLocked, null, member, member.FailedLoginAttempts, member.LockoutEndAt);
            }

            await memberRepository.SaveChangesAsync(cancellationToken);
            return new LoginResult(LoginOutcome.InvalidCredentials, null, null);
        }

        // 密碼正確：重設失敗計數與鎖定狀態（即使鎖定時效已過期，這裡也一併清空殘留欄位）
        member.FailedLoginAttempts = 0;
        member.LockoutEndAt = null;

        if (member.Status == MemberStatus.Pending)
        {
            await memberRepository.SaveChangesAsync(cancellationToken);
            return new LoginResult(LoginOutcome.MemberPending, null, member);
        }

        if (member.Status == MemberStatus.Suspended)
        {
            await memberRepository.SaveChangesAsync(cancellationToken);
            return new LoginResult(LoginOutcome.MemberSuspended, null, member);
        }

        await memberRepository.SaveChangesAsync(cancellationToken);

        var response = new LoginResponse(member.Id, member.Email, member.DisplayName, member.Status, request.ReturnUrl);
        return new LoginResult(LoginOutcome.Success, response, member);
    }
}
