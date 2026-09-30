using System.Security.Cryptography;
using System.Text;
using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemberApi.Sms;

public class SmsOtpService(MemberApiDbContext dbContext, TimeProvider timeProvider) : ISmsOtpService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public async Task<GenerateOtpResult> GenerateOtpAsync(string phoneNumber, SmsOtpPurpose purpose, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        // 作廢該號碼與該用途先前尚未消耗的既有 OTP，確保最新 OTP 單一有效
        var activeOtps = await dbContext.SmsOtps
            .Where(x => x.PhoneNumber == phoneNumber && x.Purpose == purpose && x.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var oldOtp in activeOtps)
        {
            oldOtp.Consume(now);
        }

        // 產生完整 6 碼數字（000000..999999）
        var code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
        var codeHash = ComputeSha256(code);
        var expiresAt = now.Add(DefaultTtl);

        var smsOtp = new SmsOtp
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phoneNumber,
            CodeHash = codeHash,
            Purpose = purpose,
            ExpiresAt = expiresAt,
            Attempts = 0,
            ConsumedAt = null,
            CreatedAt = now,
        };

        dbContext.SmsOtps.Add(smsOtp);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new GenerateOtpResult(code, expiresAt);
    }

    public async Task<VerifyOtpResult> VerifyOtpAsync(string phoneNumber, string code, SmsOtpPurpose purpose, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var otp = await dbContext.SmsOtps
            .Where(x => x.PhoneNumber == phoneNumber && x.Purpose == purpose && x.ConsumedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp == null)
        {
            return new VerifyOtpResult(false, "找不到有效的驗證碼");
        }

        if (otp.IsExpired(now))
        {
            return new VerifyOtpResult(false, "驗證碼已過期", IsExpired: true);
        }

        if (otp.HasReachedMaxAttempts())
        {
            return new VerifyOtpResult(false, "驗證碼嘗試次數已達上限，請重新發送", MaxAttemptsReached: true);
        }

        var inputHash = ComputeSha256(code);
        var otpHashBytes = Encoding.UTF8.GetBytes(otp.CodeHash);
        var inputHashBytes = Encoding.UTF8.GetBytes(inputHash);

        if (!CryptographicOperations.FixedTimeEquals(otpHashBytes, inputHashBytes))
        {
            otp.IncrementAttempts();
            await dbContext.SaveChangesAsync(cancellationToken);

            if (otp.HasReachedMaxAttempts())
            {
                return new VerifyOtpResult(false, "驗證碼錯誤，嘗試次數已達上限", MaxAttemptsReached: true);
            }

            var remaining = SmsOtp.MaxAttemptsAllowed - otp.Attempts;
            return new VerifyOtpResult(false, $"驗證碼錯誤，剩餘嘗試次數：{remaining}");
        }

        otp.Consume(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new VerifyOtpResult(true, null);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
