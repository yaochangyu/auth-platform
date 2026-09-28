using MemberApi.Entities;

namespace MemberApi.Contracts;

public record LoginResponse(Guid MemberId, string Email, string DisplayName, MemberStatus Status, string? ReturnUrl);
