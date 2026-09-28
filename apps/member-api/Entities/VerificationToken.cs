namespace MemberApi.Entities;

public class VerificationToken
{
    public Guid Id { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public required string Token { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
