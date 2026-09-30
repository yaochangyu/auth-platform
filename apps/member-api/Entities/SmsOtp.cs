namespace MemberApi.Entities;

public enum SmsOtpPurpose
{
    PhoneVerification = 1,
    Login = 2,
    PasswordReset = 3,
}

public class SmsOtp
{
    public const int MaxAttemptsAllowed = 5;

    public Guid Id { get; set; }

    public required string PhoneNumber { get; set; }

    public required string CodeHash { get; set; }

    public SmsOtpPurpose Purpose { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsExpired(DateTimeOffset now) => now > ExpiresAt;

    public bool HasReachedMaxAttempts() => Attempts >= MaxAttemptsAllowed;

    public void IncrementAttempts()
    {
        Attempts++;
    }

    public void Consume(DateTimeOffset now)
    {
        ConsumedAt = now;
    }
}
