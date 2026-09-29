namespace MemberApi.Entities;

public class Member
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public required string DisplayName { get; set; }

    public MemberStatus Status { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTimeOffset? LockoutEndAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
