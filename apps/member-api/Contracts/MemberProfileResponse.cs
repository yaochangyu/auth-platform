using MemberApi.Entities;

namespace MemberApi.Contracts;

public record MemberProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    MemberStatus Status,
    DateTimeOffset? EmailVerifiedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateOnly? Birthday = null,
    string? Education = null,
    string? Address = null,
    string? JobTitle = null);
