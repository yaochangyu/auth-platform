using MemberApi.Contracts;
using MemberApi.Domain;
using MemberApi.Entities;
using MemberApi.Repositories;
using Microsoft.AspNetCore.Identity;

namespace MemberApi.Handlers;

public class LoginHandler(IMemberRepository memberRepository, IPasswordHasher<Member> passwordHasher, TimeProvider timeProvider) : ILoginHandler
{
    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var member = request.Email.Contains('@')
            ? await memberRepository.FindByEmailAsync(request.Email, cancellationToken)
            : await memberRepository.FindByPhoneAsync(request.Email, cancellationToken);

        if (member is null)
        {
            return new LoginResult(LoginOutcome.InvalidCredentials, null, null);
        }

        var now = timeProvider.GetUtcNow();

        if (LoginLockoutPolicy.IsLocked(member.Lockout, now))
        {
            return new LoginResult(LoginOutcome.AccountLocked, null, member, member.FailedLoginAttempts, member.LockoutEndAt);
        }

        var passwordVerified = passwordHasher.VerifyHashedPassword(member, member.PasswordHash, request.Password) != PasswordVerificationResult.Failed;
        if (!passwordVerified)
        {
            // 列鎖內讀取-計算-寫回，避免並行請求造成 Lost Update；規則由 LoginLockoutPolicy 決定。
            var lockout = (await memberRepository.UpdateLockoutAsync(
                member.Id, state => LoginLockoutPolicy.RegisterFailure(state, now), cancellationToken))!;

            if (LoginLockoutPolicy.IsLocked(lockout, now))
            {
                return new LoginResult(LoginOutcome.AccountLocked, null, member, lockout.FailedLoginAttempts, lockout.LockoutEndAt);
            }

            return new LoginResult(LoginOutcome.InvalidCredentials, null, null);
        }

        // 密碼正確：重設失敗計數與鎖定狀態
        member.ApplyLockout(LoginLockoutPolicy.Cleared);

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
