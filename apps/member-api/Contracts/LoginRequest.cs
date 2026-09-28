namespace MemberApi.Contracts;

public record LoginRequest(string Email, string Password, string? ReturnUrl);
