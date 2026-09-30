using System.Security.Claims;
using FluentValidation;
using MemberApi.Contracts;
using MemberApi.Handlers;
using MemberApi.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MemberApi.Controllers;

[Route("api/v1/member")]
[Authorize]
public class MemberController(
    IGetMemberProfileHandler getMemberProfileHandler,
    IUpdateMemberProfileHandler updateMemberProfileHandler,
    IChangePasswordHandler changePasswordHandler,
    IListConnectedAppsHandler listConnectedAppsHandler,
    IRevokeConnectedAppHandler revokeConnectedAppHandler,
    IValidator<ChangePasswordRequest> changePasswordValidator,
    IValidator<UpdateMemberProfileRequest> updateMemberProfileValidator) : MemberApiControllerBase
{
    [HttpGet("profile")]
    [Authorize(Policy = "ProfileAccess")]
    [ProducesResponseType(typeof(MemberProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var memberId = this.CurrentMemberId();
        if (memberId == Guid.Empty)
        {
            return this.MemberNotFoundProblem();
        }

        var profile = await getMemberProfileHandler.HandleAsync(memberId, cancellationToken);
        if (profile is null)
        {
            return this.MemberNotFoundProblem();
        }

        return this.Ok(profile);
    }

    [HttpPatch("profile")]
    [Authorize(Policy = "ProfileWriteAccess")]
    [ProducesResponseType(typeof(MemberProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProfile(
        UpdateMemberProfileRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await updateMemberProfileValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return this.ToValidationProblem(validationResult, "請求參數驗證失敗");
        }

        var memberId = this.CurrentMemberId();
        if (memberId == Guid.Empty)
        {
            return this.MemberNotFoundProblem();
        }

        var result = await updateMemberProfileHandler.HandleAsync(memberId, request, cancellationToken);
        return result.Outcome switch
        {
            UpdateMemberProfileOutcome.Success => this.Ok(result.Profile),
            UpdateMemberProfileOutcome.NotFound => this.MemberNotFoundProblem(),
            UpdateMemberProfileOutcome.BirthdayConflict => this.Problem(
                type: "https://auth.1111.com.tw/errors/attribute-conflict",
                title: "生日屬性已存在且受 Write-Once 保護，不允許覆寫",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException($"未知的更新結果: {result.Outcome}"),
        };
    }

    [HttpPut("password")]
    [Authorize(Policy = "FirstPartyOnly")]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
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

    [HttpGet("connected-apps")]
    [Authorize(Policy = "FirstPartyOnly")]
    [ProducesResponseType(typeof(ConnectedAppListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListConnectedApps(CancellationToken cancellationToken)
    {
        var memberId = this.CurrentMemberId();
        var response = await listConnectedAppsHandler.HandleAsync(memberId, cancellationToken);
        return this.Ok(response);
    }

    [HttpDelete("connected-apps/{appId:guid}")]
    [Authorize(Policy = "FirstPartyOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeConnectedApp(Guid appId, CancellationToken cancellationToken)
    {
        var memberId = this.CurrentMemberId();
        var outcome = await revokeConnectedAppHandler.HandleAsync(memberId, appId, cancellationToken);
        if (outcome == RevokeConnectedAppOutcome.NotFound)
        {
            return this.Problem(
                type: "https://auth.1111.com.tw/errors/connected-app-not-found",
                title: "查無該已連結應用程式授權紀錄",
                statusCode: StatusCodes.Status404NotFound);
        }

        return this.NoContent();
    }

    private Guid CurrentMemberId()
    {
        var memberIdText = this.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? this.User.FindFirstValue("sub");
        return Guid.TryParse(memberIdText, out var id) ? id : Guid.Empty;
    }

    private ObjectResult MemberNotFoundProblem()
    {
        return this.Problem(
            type: "https://auth.1111.com.tw/errors/member-not-found",
            title: "查無此會員資料",
            statusCode: StatusCodes.Status404NotFound);
    }
}
