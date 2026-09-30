using FluentValidation;
using MemberApi.Contracts;

namespace MemberApi.Validators;

public class SendSmsOtpRequestValidator : AbstractValidator<SendSmsOtpRequest>
{
    public SendSmsOtpRequestValidator()
    {
        this.RuleFor(request => request.PhoneNumber)
            .NotEmpty().WithMessage("手機號碼不可為空。")
            .Matches(PhoneNumberValidatorRules.Pattern).WithMessage(PhoneNumberValidatorRules.ErrorMessage);
    }
}
