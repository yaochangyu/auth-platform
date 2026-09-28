using FluentValidation;
using MemberApi.Contracts;

namespace MemberApi.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        this.RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress();

        this.RuleFor(request => request.Password)
            .NotEmpty();

        this.RuleFor(request => request.ReturnUrl)
            .Must(BeAValidReturnUrl)
            .WithMessage("returnUrl 須為本地相對路徑或 .1111.com.tw 網域下的 https 網址。")
            .When(request => !string.IsNullOrEmpty(request.ReturnUrl));
    }

    private static bool BeAValidReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
        {
            return true;
        }

        if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
        {
            return uri.Host.Equals("1111.com.tw", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".1111.com.tw", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
