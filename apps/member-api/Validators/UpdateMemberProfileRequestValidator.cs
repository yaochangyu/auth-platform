using FluentValidation;
using MemberApi.Contracts;

namespace MemberApi.Validators;

public class UpdateMemberProfileRequestValidator : AbstractValidator<UpdateMemberProfileRequest>
{
    public UpdateMemberProfileRequestValidator(TimeProvider timeProvider)
    {
        this.RuleFor(request => request.Education)
            .MaximumLength(50).WithMessage("最高學歷長度不可超過 50 個字元。")
            .When(request => !string.IsNullOrEmpty(request.Education));

        this.RuleFor(request => request.Address)
            .MaximumLength(200).WithMessage("通訊地址長度不可超過 200 個字元。")
            .When(request => !string.IsNullOrEmpty(request.Address));

        this.RuleFor(request => request.JobTitle)
            .MaximumLength(50).WithMessage("目前職稱長度不可超過 50 個字元。")
            .When(request => !string.IsNullOrEmpty(request.JobTitle));

        this.RuleFor(request => request.Birthday)
            .Must(birthday => birthday <= DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime))
            .WithMessage("生日日期不可大於今天。")
            .When(request => request.Birthday.HasValue);
    }
}
