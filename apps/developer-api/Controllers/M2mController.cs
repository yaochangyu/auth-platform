using System.Security.Claims;
using DeveloperApi.Security.Hmac;
using AuthShared.Web;
using DeveloperApi.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperApi.Controllers;

// Server-to-Server 連線檢查：以 API Key + HMAC 簽章驗證，讓後端服務在整合時確認自己的金鑰與簽章計算是否正確。
[Route("api/v1/m2m")]
[Authorize(AuthenticationSchemes = HmacAuthenticationDefaults.Scheme, Policy = AuthPolicies.M2mProfile)]
public class M2mController : ApiControllerBase
{
    [HttpPost("echo")]
    [ProducesResponseType(typeof(M2mEchoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Echo(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(this.Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        return this.Ok(new M2mEchoResponse(
            this.User.FindFirstValue("client_id")!,
            Guid.Parse(this.User.FindFirstValue("api_key_id")!),
            this.User.FindFirstValue("scope")!.Split(' '),
            body.Length));
    }
}
