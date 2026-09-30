using System.Security.Cryptography;
using System.Text;
using MemberApi.Entities;
using MemberApi.Infrastructure.Persistence;
using MemberApi.Sms;
using MemberApi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace MemberApi.Tests.Sms;

public class SmsOtpServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    private static MemberApiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MemberApiDbContext>()
            .UseNpgsql(TestRunHooks.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new MemberApiDbContext(options);
    }

    [Fact]
    public async Task GenerateOtpAsync_ShouldGenerate6DigitCode_AndStoreSha256Hash()
    {
        await using var dbContext = CreateDbContext();
        var service = new SmsOtpService(dbContext, _timeProvider);
        var phone = $"09{Random.Shared.Next(10000000, 99999999)}";

        var result = await service.GenerateOtpAsync(phone, SmsOtpPurpose.PhoneVerification);

        Assert.Equal(6, result.Code.Length);
        Assert.True(int.TryParse(result.Code, out _));
        Assert.Equal(_timeProvider.GetUtcNow().AddMinutes(5), result.ExpiresAt);

        var savedOtp = await dbContext.SmsOtps.SingleOrDefaultAsync(x => x.PhoneNumber == phone);
        Assert.NotNull(savedOtp);
        Assert.Equal(phone, savedOtp.PhoneNumber);
        Assert.Equal(SmsOtpPurpose.PhoneVerification, savedOtp.Purpose);
        Assert.Equal(0, savedOtp.Attempts);
        Assert.Null(savedOtp.ConsumedAt);

        // 驗證儲存的是 SHA-256 雜湊值而非明文
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(result.Code)));
        Assert.Equal(expectedHash, savedOtp.CodeHash);
        Assert.NotEqual(result.Code, savedOtp.CodeHash);
    }

    [Fact]
    public async Task GenerateOtpAsync_WhenActiveOtpExists_ShouldInvalidatePreviousOtp()
    {
        await using var dbContext = CreateDbContext();
        var service = new SmsOtpService(dbContext, _timeProvider);
        var phone = $"09{Random.Shared.Next(10000000, 99999999)}";

        var firstResult = await service.GenerateOtpAsync(phone, SmsOtpPurpose.PhoneVerification);
        var secondResult = await service.GenerateOtpAsync(phone, SmsOtpPurpose.PhoneVerification);

        // 驗證第一組 OTP 已被標記 Consumed（作廢）
        var firstOtp = await dbContext.SmsOtps.FirstAsync(x => x.PhoneNumber == phone && x.CodeHash != Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secondResult.Code))));
        Assert.NotNull(firstOtp.ConsumedAt);

        // 嘗試拿第一組已作廢的 code 驗證應失敗
        var verifyFirst = await service.VerifyOtpAsync(phone, firstResult.Code, SmsOtpPurpose.PhoneVerification);
        Assert.False(verifyFirst.Success);

        // 驗證第二組 code 應成功
        var verifySecond = await service.VerifyOtpAsync(phone, secondResult.Code, SmsOtpPurpose.PhoneVerification);
        Assert.True(verifySecond.Success);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenCodeIsCorrect_ShouldSucceedAndMarkConsumed()
    {
        await using var dbContext = CreateDbContext();
        var service = new SmsOtpService(dbContext, _timeProvider);
        var phone = $"09{Random.Shared.Next(10000000, 99999999)}";

        var generated = await service.GenerateOtpAsync(phone, SmsOtpPurpose.PhoneVerification);
        var verifyResult = await service.VerifyOtpAsync(phone, generated.Code, SmsOtpPurpose.PhoneVerification);

        Assert.True(verifyResult.Success);
        Assert.Null(verifyResult.ErrorMessage);

        var savedOtp = await dbContext.SmsOtps.SingleAsync(x => x.PhoneNumber == phone);
        Assert.NotNull(savedOtp.ConsumedAt);

        // 重播防護：再次驗證應失敗（因為已被 Consumed）
        var replayResult = await service.VerifyOtpAsync(phone, generated.Code, SmsOtpPurpose.PhoneVerification);
        Assert.False(replayResult.Success);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenExpired_ShouldFailWithIsExpiredTrue()
    {
        await using var dbContext = CreateDbContext();
        var service = new SmsOtpService(dbContext, _timeProvider);
        var phone = $"09{Random.Shared.Next(10000000, 99999999)}";

        var generated = await service.GenerateOtpAsync(phone, SmsOtpPurpose.PhoneVerification);

        // 時間前進 5 分鐘又 1 秒
        _timeProvider.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        var verifyResult = await service.VerifyOtpAsync(phone, generated.Code, SmsOtpPurpose.PhoneVerification);

        Assert.False(verifyResult.Success);
        Assert.True(verifyResult.IsExpired);
        Assert.Equal("驗證碼已過期", verifyResult.ErrorMessage);
    }

    [Fact]
    public async Task VerifyOtpAsync_WhenFailedUpTo5Times_ShouldLockAndPreventFurtherAttempts()
    {
        await using var dbContext = CreateDbContext();
        var service = new SmsOtpService(dbContext, _timeProvider);
        var phone = $"09{Random.Shared.Next(10000000, 99999999)}";

        var generated = await service.GenerateOtpAsync(phone, SmsOtpPurpose.PhoneVerification);

        // 連續輸入 4 次錯誤驗證碼
        for (var i = 1; i <= 4; i++)
        {
            var res = await service.VerifyOtpAsync(phone, "000000", SmsOtpPurpose.PhoneVerification);
            Assert.False(res.Success);
            Assert.False(res.MaxAttemptsReached);
            Assert.Contains($"剩餘嘗試次數：{5 - i}", res.ErrorMessage);
        }

        // 第 5 次錯誤
        var fifthResult = await service.VerifyOtpAsync(phone, "000000", SmsOtpPurpose.PhoneVerification);
        Assert.False(fifthResult.Success);
        Assert.True(fifthResult.MaxAttemptsReached);

        // 第 6 次嘗試，即使輸入正確的 code 也必須遭拒絕
        var sixthResult = await service.VerifyOtpAsync(phone, generated.Code, SmsOtpPurpose.PhoneVerification);
        Assert.False(sixthResult.Success);
        Assert.True(sixthResult.MaxAttemptsReached);
    }
}
