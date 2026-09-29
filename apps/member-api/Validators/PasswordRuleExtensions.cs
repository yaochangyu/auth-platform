using FluentValidation;

namespace MemberApi.Validators;

public static class PasswordRuleExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeStrongPassword<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .Length(8, 128)
            .Matches("[A-Z]").WithMessage("密碼須包含至少一個大寫字母。")
            .Matches("[a-z]").WithMessage("密碼須包含至少一個小寫字母。")
            .Matches("[0-9]").WithMessage("密碼須包含至少一個數字。")
            .Matches(@"[^A-Za-z0-9]").WithMessage("密碼須包含至少一個特殊符號。");
    }
}
