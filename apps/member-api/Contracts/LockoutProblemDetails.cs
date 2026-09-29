using Microsoft.AspNetCore.Mvc;

namespace MemberApi.Contracts;

public class LockoutProblemDetails : ProblemDetails
{
    public int FailedLoginAttempts { get; set; }

    public DateTimeOffset? LockoutEndAt { get; set; }
}
