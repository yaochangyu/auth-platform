using DeveloperApi.Contracts;
using DeveloperApi.Repositories;
using FluentValidation;

namespace DeveloperApi.Validators;

public class ApiKeyRequestValidator : AbstractValidator<ApiKeyRequest>
{
    public ApiKeyRequestValidator(TimeProvider timeProvider)
    {
        this.RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        this.RuleFor(request => request.Environment).IsInEnum();
        this.RuleFor(request => request.Scopes).NotEmpty().WithMessage("至少要選擇一個範疇。");
        this.RuleForEach(request => request.Scopes).Must(scope => ApiKeyRepository.AllowedScopes.Contains(scope))
            .WithMessage($"只能綁定這些範疇：{string.Join("、", ApiKeyRepository.AllowedScopes)}。");
        this.RuleFor(request => request.ExpiresAt).Must(expiresAt => expiresAt > timeProvider.GetUtcNow())
            .WithMessage("到期時間必須晚於現在。").When(request => request.ExpiresAt is not null);
    }
}
