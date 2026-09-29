using AdminApi.Contracts;
using AuthShared;
using FluentValidation;

namespace AdminApi.Validators;

public class StatusChangeRequestValidator : AbstractValidator<StatusChangeRequest>
{
    public StatusChangeRequestValidator()
    {
        // 管理員只在 Active 與 Suspended 之間切換；PendingReview 是新專案的初始審核狀態，不能被設回去。
        this.RuleFor(request => request.Status).Must(status => status is ApplicationStatus.Active or ApplicationStatus.Suspended)
            .WithMessage("狀態只能設為 Active 或 Suspended。");
        this.RuleFor(request => request.Reason).MaximumLength(500);
        this.RuleFor(request => request.Reason).NotEmpty().WithMessage("停用應用程式必須填寫原因，供稽核追溯。")
            .When(request => request.Status == ApplicationStatus.Suspended);
    }
}
