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

        // 鎖定期間內一律直接拒絕，即使密碼正確也不進行雜湊比對；
        // 鎖定時效已過期的「重設為全新計數起點」邏輯已下推至 RegisterFailedLoginAsync 的原子 SQL 內處理。
        if (member.LockoutEndAt is not null && member.LockoutEndAt > now)
        {
            return new LoginResult(LoginOutcome.AccountLocked, null, member, member.FailedLoginAttempts, member.LockoutEndAt);
        }

        var passwordVerified = passwordHasher.VerifyHashedPassword(member, member.PasswordHash, request.Password) != PasswordVerificationResult.Failed;
        if (!passwordVerified)
        {
            // 原子 UPDATE ... RETURNING：避免多個並行請求各自讀取-遞增-寫回造成 Lost Update，
            // 確保並行密碼錯誤時失敗計數精確累加、鎖定判斷不失準。
            var (failedLoginAttempts, lockoutEndAt) =
                (await memberRepository.RegisterFailedLoginAsync(member.Id, now, MaxFailedAttempts, LockoutDuration, cancellationToken))!.Value;

            if (lockoutEndAt is not null && lockoutEndAt > now)
            {
                return new LoginResult(LoginOutcome.AccountLocked, null, member, failedLoginAttempts, lockoutEndAt);
            }

            return new LoginResult(LoginOutcome.InvalidCredentials, null, null);
        }

        // 密碼正確：重設失敗計數與鎖定狀態
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
