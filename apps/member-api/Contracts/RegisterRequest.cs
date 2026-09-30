namespace MemberApi.Contracts;

public record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string DisplayName,
    string? PhoneNumber = null);
