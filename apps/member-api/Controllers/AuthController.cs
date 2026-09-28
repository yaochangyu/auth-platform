using FluentValidation;
using FluentValidation.Results;
using MemberApi.Contracts;
using MemberApi.Handlers;
using Microsoft.AspNetCore.Mvc;

namespace MemberApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IRegisterMemberHandler registerMemberHandler,
    IVerifyEmailHandler verifyEmailHandler,
    IValidator<RegisterRequest> registerValidator,
    IValidator<VerifyEmailRequest> verifyEmailValidator) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await registerValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.BadRequest(ToValidationProblem(validationResult, "請求參數驗證失敗"));
        }

        var result = await registerMemberHandler.RegisterAsync(request, cancellationToken);
        if (result.Outcome == RegisterOutcome.EmailAlreadyActive)
        {
            return this.Problem(
                type: "https://auth.1111.com.tw/errors/email-already-active",
                title: "此 Email 已完成驗證並處於已啟用 (Active) 狀態，無法重複註冊",
                statusCode: StatusCodes.Status409Conflict);
        }

        return this.StatusCode(StatusCodes.Status201Created, result.Response);
    }

    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(VerifyEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await verifyEmailValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.BadRequest(ToValidationProblem(validationResult, "驗證權杖格式錯誤"));
        }

        var result = await verifyEmailHandler.VerifyAsync(request, cancellationToken);
        return result.Outcome switch
        {
            VerifyEmailOutcome.Verified => this.Ok(result.Response),
            VerifyEmailOutcome.TokenNotFound => this.Problem(
                type: "https://auth.1111.com.tw/errors/verification-token-not-found",
                title: "查無此驗證權杖",
                statusCode: StatusCodes.Status404NotFound),
            _ => this.Problem(
                type: "https://auth.1111.com.tw/errors/verification-token-expired",
                title: "驗證權杖已過期或已遭使用",
                statusCode: StatusCodes.Status410Gone),
        };
    }

    private static ValidationProblemDetails ToValidationProblem(ValidationResult result, string title)
    {
        var errors = result.Errors
            .GroupBy(error => ToCamelCase(error.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Type = "https://auth.1111.com.tw/errors/validation-failed",
            Title = title,
            Status = StatusCodes.Status400BadRequest,
        };
    }

    private static string ToCamelCase(string value)
    {
        return string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
    }
}
