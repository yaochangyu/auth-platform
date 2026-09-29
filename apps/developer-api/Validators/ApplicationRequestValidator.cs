using DeveloperApi.Contracts;
using FluentValidation;

namespace DeveloperApi.Validators;

public class ApplicationRequestValidator : AbstractValidator<ApplicationRequest>
{
    public ApplicationRequestValidator()
    {
        this.RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        this.RuleFor(request => request.Description).NotEmpty().MaximumLength(500);
        this.RuleFor(request => request.ContactEmail).NotEmpty().EmailAddress().MaximumLength(254);
        this.RuleFor(request => request.LogoUrl)
            .Must(BeHttpUrl).WithMessage("網址須為 http 或 https 的絕對網址。")
            .MaximumLength(2048)
            .When(request => !string.IsNullOrEmpty(request.LogoUrl));
        this.RuleFor(request => request.HomepageUrl)
            .Must(BeHttpUrl).WithMessage("網址須為 http 或 https 的絕對網址。")
            .MaximumLength(2048)
            .When(request => !string.IsNullOrEmpty(request.HomepageUrl));
    }

    // 只允許 http(s)，擋掉 javascript:、data: 等會在前端被當成連結或圖片來源執行的協定。
    private static bool BeHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
