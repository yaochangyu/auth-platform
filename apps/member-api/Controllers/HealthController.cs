using MemberApi.Contracts;
using MemberApi.Handlers;
using Microsoft.AspNetCore.Mvc;

namespace MemberApi.Controllers;

[ApiController]
[Route("health")]
public class HealthController(IHealthCheckHandler healthCheckHandler) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CheckHealth(CancellationToken cancellationToken)
    {
        var result = await healthCheckHandler.CheckAsync(cancellationToken);

        if (result.Status == HealthStatus.Unhealthy)
        {
            return this.Problem(
                title: "服務或相依資料庫異常，無法提供正常服務",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return this.Ok(result);
    }
}
