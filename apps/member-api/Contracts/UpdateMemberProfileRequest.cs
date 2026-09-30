namespace MemberApi.Contracts;

public record UpdateMemberProfileRequest(
    DateOnly? Birthday = null,
    string? Education = null,
    string? Address = null,
    string? JobTitle = null);
