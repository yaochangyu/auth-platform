using System.Security.Claims;
using MemberApi.Entities;

namespace MemberApi.Security;

public static class MemberClaimsFactory
{
    public static ClaimsPrincipal Build(Member member, string authenticationScheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, member.Id.ToString()),
            new(ClaimTypes.Email, member.Email),
            new(ClaimTypes.Name, member.DisplayName),
            new("status", member.Status.ToString()),
            new(SecurityStampClaimTypes.ClaimType, member.SecurityStamp),
        };
        var identity = new ClaimsIdentity(claims, authenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
