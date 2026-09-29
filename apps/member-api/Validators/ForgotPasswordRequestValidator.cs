using FluentValidation;
using MemberApi.Contracts;

namespace MemberApi.Validators;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        this.RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);
    }
}
