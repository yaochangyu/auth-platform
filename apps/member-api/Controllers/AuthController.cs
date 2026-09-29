using FluentValidation;
using FluentValidation.Results;
using MemberApi.Contracts;
using MemberApi.Handlers;
using MemberApi.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MemberApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IRegisterMemberHandler registerMemberHandler,
    IVerifyEmailHandler verifyEmailHandler,
    ILoginHandler loginHandler,
    IForgotPasswordHandler forgotPasswordHandler,
    IResetPasswordHandler resetPasswordHandler,
    IValidator<RegisterRequest> registerValidator,
    IValidator<VerifyEmailRequest> verifyEmailValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<ForgotPasswordRequest> forgotPasswordValidator,
    IValidator<ResetPasswordRequest> resetPasswordValidator) : MemberApiControllerBase
{
    private const string ForgotPasswordAcceptedMessage = "若該信箱已在平台註冊，系統將寄出重設密碼說明信件，請於 15 分鐘內完成重設。";
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

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(LockoutProblemDetails), StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ToValidationProblem(validationResult, "請求參數驗證失敗");
        }

        var result = await loginHandler.LoginAsync(request, cancellationToken);
        if (result.Outcome == LoginOutcome.InvalidCredentials)
        {
            return this.Problem(
                type: "https://auth.1111.com.tw/errors/invalid-credentials",
                title: "Email 或密碼不正確",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (result.Outcome == LoginOutcome.AccountLocked)
        {
            var lockoutProblem = new LockoutProblemDetails
            {
                Type = "https://auth.1111.com.tw/errors/account-locked",
                Title = "帳號已因連續登入失敗遭暫時鎖定",
                Status = StatusCodes.Status423Locked,
                FailedLoginAttempts = result.FailedLoginAttempts!.Value,
                LockoutEndAt = result.LockoutEndAt,
            };
            return new ObjectResult(lockoutProblem) { StatusCode = StatusCodes.Status423Locked };
        }

        if (result.Outcome == LoginOutcome.MemberPending)
        {
            return this.Problem(
                type: "https://auth.1111.com.tw/errors/member-not-active",
                title: "會員身分尚未啟用，請先完成信箱驗證",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (result.Outcome == LoginOutcome.MemberSuspended)
        {
            return this.Problem(
                type: "https://auth.1111.com.tw/errors/member-suspended",
                title: "會員帳號已被停權",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var principal = MemberClaimsFactory.Build(result.Member!, CookieAuthenticationDefaults.AuthenticationScheme);
        await this.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return this.Ok(result.Response);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await this.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return this.Ok(new LogoutResponse("已成功登出並註銷會話。"));
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await forgotPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ToValidationProblem(validationResult, "請求參數驗證失敗");
        }

        var outcome = await forgotPasswordHandler.HandleAsync(request, cancellationToken);
        if (outcome == ForgotPasswordOutcome.RateLimited)
        {
            return this.Problem(
                type: "https://auth.1111.com.tw/errors/too-many-requests",
                title: "請求頻率過高，請稍候再試",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        return this.Ok(new ForgotPasswordResponse(ForgotPasswordAcceptedMessage));
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ResetPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await resetPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ToValidationProblem(validationResult, "請求參數驗證失敗");
        }

        var outcome = await resetPasswordHandler.HandleAsync(request, cancellationToken);
        return outcome switch
        {
            ResetPasswordOutcome.Success => this.Ok(new ResetPasswordResponse(
                "密碼重設成功，所有歷史裝置之會話已立即失效，請使用新密碼重新登入。")),
            ResetPasswordOutcome.TokenNotFound => this.Problem(
                type: "https://auth.1111.com.tw/errors/verification-token-not-found",
                title: "查無此驗證權杖",
                statusCode: StatusCodes.Status404NotFound),
            _ => this.Problem(
                type: "https://auth.1111.com.tw/errors/verification-token-expired",
                title: "驗證權杖已過期或已遭使用",
                statusCode: StatusCodes.Status410Gone),
        };
    }

}
