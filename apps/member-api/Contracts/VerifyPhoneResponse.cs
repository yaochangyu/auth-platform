namespace MemberApi.Contracts;

public record VerifyPhoneResponse(bool Success, string Message, string PhoneNumber, DateTimeOffset? VerifiedAt = null);
