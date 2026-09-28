using MemberApi.Entities;

namespace MemberApi.Contracts;

public record VerifyEmailResponse(Guid MemberId, string Email, MemberStatus Status, string Message);
