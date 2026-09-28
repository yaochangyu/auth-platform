using System.Security.Cryptography;
using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Repositories;
using Microsoft.AspNetCore.Identity;

namespace MemberApi.Handlers;

public class RegisterMemberHandler(
    IMemberRepository memberRepository,
    IPasswordHasher<Member> passwordHasher,
    TimeProvider timeProvider) : IRegisterMemberHandler
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var existingMember = await memberRepository.FindByEmailAsync(request.Email, cancellationToken);

        Member member;
        if (existingMember is null)
        {
            member = new Member
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                DisplayName = request.DisplayName,
                PasswordHash = string.Empty,
                Status = MemberStatus.Pending,
                CreatedAt = now,
            };
            member.PasswordHash = passwordHasher.HashPassword(member, request.Password);
            memberRepository.AddMember(member);
        }
        else if (existingMember.Status != MemberStatus.Pending)
        {
            return new RegisterResult(RegisterOutcome.EmailAlreadyActive, null);
        }
        else
        {
            // 靜默重寄驗證信，維持原密碼雜湊不變
            member = existingMember;
        }

        var token = GenerateToken();
        memberRepository.AddVerificationToken(new VerificationToken
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            Token = token,
            ExpiresAt = now + TokenLifetime,
            CreatedAt = now,
        });

        memberRepository.AddOutboxMessage(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            ToEmail = member.Email,
            Subject = "請驗證您的會員信箱",
            Body = $"請點擊連結以驗證您的信箱：https://auth.1111.com.tw/verify-email?token={token}",
            CreatedAt = now,
        });

        await memberRepository.SaveChangesAsync(cancellationToken);

        var response = new RegisterResponse(
            member.Id,
            member.Email,
            MemberStatus.Pending,
            "註冊成功，請前往信箱點擊驗證連結啟用會員身分。");

        return new RegisterResult(RegisterOutcome.Created, response);
    }

    private static string GenerateToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}
