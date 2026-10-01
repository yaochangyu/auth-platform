using MemberApi.Contracts;
using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using MemberApi.Sms;
using Microsoft.EntityFrameworkCore;

namespace MemberApi.Handlers;

public enum VerifyPhoneOutcome
{
    Success,
    InvalidCode,
    Expired,
    MaxAttemptsReached,
    PhoneAlreadyBound,
    MemberNotFound,
}

public record VerifyPhoneResult(
    VerifyPhoneOutcome Outcome,
    string? Message = null,
    DateTimeOffset? VerifiedAt = null);

public class VerifyPhoneHandler(
    ISmsOtpService smsOtpService,
    MemberApiDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<VerifyPhoneResult> HandleAsync(VerifyPhoneRequest request, Guid? currentMemberId, CancellationToken cancellationToken)
    {
        var verifyOtpResult = await smsOtpService.VerifyOtpAsync(
            request.PhoneNumber,
            request.Code,
            SmsOtpPurpose.PhoneVerification,
            cancellationToken);

        if (!verifyOtpResult.Success)
        {
            if (verifyOtpResult.IsExpired)
            {
                return new VerifyPhoneResult(VerifyPhoneOutcome.Expired, verifyOtpResult.ErrorMessage);
            }

            if (verifyOtpResult.MaxAttemptsReached)
            {
                return new VerifyPhoneResult(VerifyPhoneOutcome.MaxAttemptsReached, verifyOtpResult.ErrorMessage);
            }

            return new VerifyPhoneResult(VerifyPhoneOutcome.InvalidCode, verifyOtpResult.ErrorMessage);
        }

        var now = timeProvider.GetUtcNow();

        if (currentMemberId.HasValue)
        {
            var phoneConflict = await dbContext.Members
                .AnyAsync(m => m.PhoneNumber == request.PhoneNumber && m.Id != currentMemberId.Value, cancellationToken);

            if (phoneConflict)
            {
                return new VerifyPhoneResult(VerifyPhoneOutcome.PhoneAlreadyBound, "此手機號碼已被其他會員帳號綁定。");
            }

            var member = await dbContext.Members.FindAsync([currentMemberId.Value], cancellationToken);
            if (member is null)
            {
                return new VerifyPhoneResult(VerifyPhoneOutcome.MemberNotFound, "查無此會員帳號。");
            }

            member.PhoneNumber = request.PhoneNumber;
            member.PhoneVerifiedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new VerifyPhoneResult(VerifyPhoneOutcome.Success, "手機號碼驗證並綁定成功。", now);
        }

        return new VerifyPhoneResult(VerifyPhoneOutcome.Success, "手機號碼驗證成功。", now);
    }
}
