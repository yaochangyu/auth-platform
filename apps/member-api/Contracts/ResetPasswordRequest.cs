namespace MemberApi.Contracts;

public record ResetPasswordRequest(string VerificationToken, string NewPassword, string ConfirmPassword);
