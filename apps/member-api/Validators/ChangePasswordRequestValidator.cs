using FluentValidation;
using MemberApi.Contracts;

namespace MemberApi.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        this.RuleFor(request => request.CurrentPassword)
            .NotEmpty();

        this.RuleFor(request => request.NewPassword)
            .MustBeStrongPassword();

        this.RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.NewPassword).WithMessage("確認密碼須與新密碼一致。");
    }
}
