using System.Security.Claims;
using FluentValidation;
using MemberApi.Contracts;
using MemberApi.Handlers;
using MemberApi.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MemberApi.Controllers;

[Route("api/v1/member")]
[Authorize]
public class MemberController(
    IGetMemberProfileHandler getMemberProfileHandler,
    IChangePasswordHandler changePasswordHandler,
    IValidator<ChangePasswordRequest> changePasswordValidator) : MemberApiControllerBase
{
    [HttpGet("profile")]
    [ProducesResponseType(typeof(MemberProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var memberId = this.CurrentMemberId();
        var profile = await getMemberProfileHandler.HandleAsync(memberId, cancellationToken);
        return this.Ok(profile);
    }

    [HttpPut("password")]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ToValidationProblem(validationResult, "請求參數驗證失敗");
        }

        var memberId = this.CurrentMemberId();
        var result = await changePasswordHandler.HandleAsync(memberId, request, cancellationToken);
        if (result.Outcome == ChangePasswordOutcome.InvalidCurrentPassword)
        {
            return this.Problem(
                type: "https://auth.1111.com.tw/errors/invalid-credentials",
                title: "目前密碼不正確",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // 重新發行「當前」裝置的 Session Cookie，帶入剛刷新的安全戳記，確保操作裝置維持登入；
        // 其他裝置持有的舊 Cookie 因戳記不符，會在下次請求時被 OnValidatePrincipal 拒絕。
        var principal = MemberClaimsFactory.Build(result.Member!, CookieAuthenticationDefaults.AuthenticationScheme);
        await this.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return this.Ok(new ChangePasswordResponse("密碼變更成功，已更新安全戳記並廢止其他歷史登入會話。"));
    }

    private Guid CurrentMemberId()
    {
        var memberIdText = this.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(memberIdText!);
    }
}
