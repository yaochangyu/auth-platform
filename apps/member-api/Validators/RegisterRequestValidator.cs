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
            .MustBeStrongPassword();

        this.RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.Password).WithMessage("確認密碼須與密碼一致。");

        this.RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(50);

        this.RuleFor(request => request.PhoneNumber)
            .Matches(PhoneNumberValidatorRules.Pattern)
            .WithMessage(PhoneNumberValidatorRules.ErrorMessage)
            .When(request => !string.IsNullOrEmpty(request.PhoneNumber));
    }
}
