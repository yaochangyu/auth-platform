using FluentValidation;
using MemberApi.Contracts;

namespace MemberApi.Validators;

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        this.RuleFor(request => request.VerificationToken)
            .NotEmpty();
    }
}
