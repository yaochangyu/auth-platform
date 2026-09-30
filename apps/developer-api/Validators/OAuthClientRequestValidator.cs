using DeveloperApi.Contracts;
using DeveloperApi.Repositories;
using FluentValidation;

namespace DeveloperApi.Validators;

public class OAuthClientRequestValidator : AbstractValidator<OAuthClientRequest>
{
    private const int MaxUris = 10;

    public OAuthClientRequestValidator()
    {
        this.RuleFor(request => request.ClientType).IsInEnum();

        this.RuleFor(request => request.RedirectUris).NotNull().Must(uris => uris.Count <= MaxUris)
            .WithMessage($"最多只能設定 {MaxUris} 個網址。").Must(BeDistinct).WithMessage("網址不可重複。");

        this.When(request => request.ClientType == OAuthClientType.Public, () =>
        {
            this.RuleForEach(request => request.RedirectUris).Must(BeHttpsUri)
                .WithMessage("公用客戶端（Public Client）的網址須為 https 的官方深層連結（Universal Links / App Links），不可使用 http 或自訂 scheme，且不可含 # 片段或帳號密碼。");
            this.RuleForEach(request => request.PostLogoutRedirectUris).Must(BeHttpsUri)
                .WithMessage("公用客戶端（Public Client）的網址須為 https 的官方網址，不可使用 http 或自訂 scheme，且不可含 # 片段或帳號密碼。");
        });

        this.When(request => request.ClientType == OAuthClientType.Confidential, () =>
        {
            this.RuleForEach(request => request.RedirectUris).Must(BeAllowedUri)
                .WithMessage("網址須為 https 的絕對網址（本機開發可用 http://localhost），且不可含 # 片段或帳號密碼。");
            this.RuleForEach(request => request.PostLogoutRedirectUris).Must(BeAllowedUri)
                .WithMessage("網址須為 https 的絕對網址（本機開發可用 http://localhost），且不可含 # 片段或帳號密碼。");
        });

        this.RuleFor(request => request.PostLogoutRedirectUris).NotNull().Must(uris => uris.Count <= MaxUris)
            .WithMessage($"最多只能設定 {MaxUris} 個網址。").Must(BeDistinct).WithMessage("網址不可重複。");

        this.RuleFor(request => request.Scopes).NotEmpty().WithMessage("至少要選擇一個範疇。").Must(BeDistinct).WithMessage("範疇不可重複。");
        this.RuleForEach(request => request.Scopes).Must(scope => OAuthClientRepository.AllowedScopes.Contains(scope))
            .WithMessage($"只能申請這些範疇：{string.Join("、", OAuthClientRepository.AllowedScopes)}。");
    }

    private static bool BeDistinct(IReadOnlyList<string>? values) =>
        values is null || values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Count;

    // 原生 App 公用客戶端強制 HTTPS 官方網域（iOS Universal Links / Android App Links）。
    private static bool BeHttpsUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || value!.Contains('*') || value.Contains('#') || uri.UserInfo.Length > 0)
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps;
    }

    // 嚴格比對（不支援萬用字元）：https，或本機回送位址的 http；不可帶 # 片段或帳號密碼。
    private static bool BeAllowedUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || value!.Contains('*') || value.Contains('#') || uri.UserInfo.Length > 0)
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
    }
}
