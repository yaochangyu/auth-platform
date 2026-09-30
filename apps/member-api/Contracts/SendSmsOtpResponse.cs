namespace MemberApi.Contracts;

public record SendSmsOtpResponse(string Message, int RetryAfterSeconds);
