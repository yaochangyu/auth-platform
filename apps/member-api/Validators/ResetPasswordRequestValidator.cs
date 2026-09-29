using FluentValidation;
using MemberApi.Contracts;

namespace MemberApi.Validators;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        this.RuleFor(request => request.VerificationToken)
            .NotEmpty();

        this.RuleFor(request => request.NewPassword)
            .MustBeStrongPassword();

        this.RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.NewPassword).WithMessage("確認密碼須與新密碼一致。");
    }
}
