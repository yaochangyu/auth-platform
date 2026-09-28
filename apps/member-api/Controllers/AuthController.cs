using FluentValidation;
using FluentValidation.Results;
using MemberApi.Contracts;
using MemberApi.Handlers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
            return this.ToValidationProblem(validationResult, "請求參數驗證失敗");
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await verifyEmailValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ToValidationProblem(validationResult, "驗證權杖格式錯誤");
        }

        var result = await verifyEmailHandler.VerifyAsync(request, cancellationToken);
        return result.Outcome switch
        {
            VerifyEmailOutcome.Verified => this.Ok(result.Response),
            VerifyEmailOutcome.TokenNotFound => this.Problem(
                type: "https://auth.1111.com.tw/errors/verification-token-not-found",
                title: "查無此驗證權杖",
                statusCode: StatusCodes.Status404NotFound),
            VerifyEmailOutcome.MemberNotPending => this.Problem(
                type: "https://auth.1111.com.tw/errors/member-not-pending",
                title: "會員目前狀態不允許進行信箱驗證",
                statusCode: StatusCodes.Status409Conflict),
            _ => this.Problem(
                type: "https://auth.1111.com.tw/errors/verification-token-expired",
                title: "驗證權杖已過期或已遭使用",
                statusCode: StatusCodes.Status410Gone),
        };
    }

    private ActionResult ToValidationProblem(ValidationResult result, string title)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in result.Errors)
        {
            modelState.AddModelError(ToCamelCase(error.PropertyName), error.ErrorMessage);
        }

        var problemResult = (ObjectResult)this.ValidationProblem(modelState);
        if (problemResult.Value is ValidationProblemDetails problemDetails)
        {
            problemDetails.Title = title;
            problemDetails.Type = "https://auth.1111.com.tw/errors/validation-failed";
        }

        return problemResult;
    }

    private static string ToCamelCase(string value)
    {
        return string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
    }
}
