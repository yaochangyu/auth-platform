using MemberApi.Entities;

namespace MemberApi.Contracts;

public record RegisterResponse(Guid MemberId, string Email, MemberStatus Status, string Message);
