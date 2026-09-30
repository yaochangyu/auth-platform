using FluentValidation;
using MemberApi.Contracts;

namespace MemberApi.Validators;

public class VerifyPhoneRequestValidator : AbstractValidator<VerifyPhoneRequest>
{
    public VerifyPhoneRequestValidator()
    {
        this.RuleFor(request => request.PhoneNumber)
            .NotEmpty().WithMessage("手機號碼不可為空。")
            .Matches(PhoneNumberValidatorRules.Pattern).WithMessage(PhoneNumberValidatorRules.ErrorMessage);

        this.RuleFor(request => request.Code)
            .NotEmpty().WithMessage("驗證碼不可為空。")
            .Matches(@"^[0-9]{6}$").WithMessage("驗證碼必須為 6 碼數字。");
    }
}
