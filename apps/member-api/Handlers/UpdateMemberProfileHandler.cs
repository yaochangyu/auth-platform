using MemberApi.Contracts;
using MemberApi.Repositories;

namespace MemberApi.Handlers;

public class UpdateMemberProfileHandler(
    IMemberRepository memberRepository,
    TimeProvider timeProvider) : IUpdateMemberProfileHandler
{
    public async Task<UpdateMemberProfileResult> HandleAsync(
        Guid memberId,
        UpdateMemberProfileRequest request,
        CancellationToken cancellationToken)
    {
        var member = await memberRepository.FindByIdAsync(memberId, cancellationToken);
        if (member is null)
        {
            return new UpdateMemberProfileResult(UpdateMemberProfileOutcome.NotFound);
        }

        // Write-Once 防弊防護：生日一旦已填寫，不允許覆寫不同值
        if (request.Birthday.HasValue)
        {
            if (member.Birthday.HasValue && member.Birthday.Value != request.Birthday.Value)
            {
                return new UpdateMemberProfileResult(UpdateMemberProfileOutcome.BirthdayConflict);
            }

            member.Birthday = request.Birthday.Value;
        }

        // 自由流動欄位增量更新
        if (request.Education != null)
        {
            member.Education = request.Education;
        }

        if (request.Address != null)
        {
            member.Address = request.Address;
        }

        if (request.JobTitle != null)
        {
            member.JobTitle = request.JobTitle;
        }

        member.UpdatedAt = timeProvider.GetUtcNow();
        await memberRepository.SaveChangesAsync(cancellationToken);

        var response = new MemberProfileResponse(
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

        return new UpdateMemberProfileResult(UpdateMemberProfileOutcome.Success, response);
    }
}
