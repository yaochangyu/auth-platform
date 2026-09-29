using AdminApi.Contracts;
using AdminApi.Infrastructure;
using AuthShared.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminApi.Controllers;

[Route("api/v1/admin/audit-logs")]
[Authorize(Policy = AuthPolicies.Admin)]
public class AuditLogsController(AdminApiDbContext dbContext) : ApiControllerBase
{
    private const int MaxPageSize = 100;

    // 稽核紀錄只提供查詢，沒有任何修改或刪除端點。
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? action,
        [FromQuery] string? targetId,
        [FromQuery] Guid? actorMemberId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = dbContext.AuditLogs.AsNoTracking()
            .Where(log => action == null || log.Action == action)
            .Where(log => targetId == null || log.TargetId == targetId)
            .Where(log => actorMemberId == null || log.ActorMemberId == actorMemberId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(log => log.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return this.Ok(new AuditLogListResponse([.. items.Select(AuditLogResponse.From)], total, page, pageSize));
    }
}
