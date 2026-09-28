using FluentValidation;
using MemberApi.Contracts;

namespace MemberApi.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        this.RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

        this.RuleFor(request => request.Password)
            .NotEmpty()
            .Length(8, 128)
            .Matches("[A-Z]").WithMessage("密碼須包含至少一個大寫字母。")
            .Matches("[a-z]").WithMessage("密碼須包含至少一個小寫字母。")
            .Matches("[0-9]").WithMessage("密碼須包含至少一個數字。")
            .Matches(@"[^A-Za-z0-9]").WithMessage("密碼須包含至少一個特殊符號。");

        this.RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.Password).WithMessage("確認密碼須與密碼一致。");

        this.RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(50);
    }
}
