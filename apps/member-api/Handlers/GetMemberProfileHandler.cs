using MemberApi.Contracts;
using MemberApi.Repositories;

namespace MemberApi.Handlers;

public class GetMemberProfileHandler(IMemberRepository memberRepository) : IGetMemberProfileHandler
{
    public async Task<MemberProfileResponse?> HandleAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var member = await memberRepository.FindByIdAsync(memberId, cancellationToken);
        if (member is null)
        {
            return null;
        }

        return new MemberProfileResponse(
            member.Id,
            member.Email,
            member.DisplayName,
            member.Status,
            member.EmailVerifiedAt,
            member.CreatedAt,
            member.UpdatedAt,
            member.Birthday,
            member.Education,
            member.Address,
            member.JobTitle);
    }
}
