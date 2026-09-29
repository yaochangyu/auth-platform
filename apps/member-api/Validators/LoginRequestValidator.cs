using FluentValidation;
using MemberApi.Contracts;

namespace MemberApi.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    // 容器/本機環境沒有 TLS 終止（Auth:RequireHttps=false）時，回跳網址也是 http；正式環境維持只接受 https。
    private readonly bool _allowHttp;

    public LoginRequestValidator(IConfiguration configuration)
    {
        this._allowHttp = !configuration.GetValue("Auth:RequireHttps", true);

        this.RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress();

        this.RuleFor(request => request.Password)
            .NotEmpty();

        this.RuleFor(request => request.ReturnUrl)
            .Must(this.BeAValidReturnUrl)
            .WithMessage("returnUrl 須為本地相對路徑或 .1111.com.tw 網域下的 https 網址（僅在 Auth:RequireHttps 為 false 的本機/容器環境才接受 http）。")
            .When(request => !string.IsNullOrEmpty(request.ReturnUrl));
    }

    private bool BeAValidReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
        {
            return true;
        }

        if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || (this._allowHttp && uri.Scheme == Uri.UriSchemeHttp)))
        {
            return uri.Host.Equals("1111.com.tw", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".1111.com.tw", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
