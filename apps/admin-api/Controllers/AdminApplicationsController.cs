using AdminApi.Contracts;
using AdminApi.Infrastructure;
using AdminApi.Services;
using AuthShared;
using AuthShared.Web;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AdminApi.Controllers;

[Route("api/v1/admin/applications")]
[Authorize(Policy = AuthPolicies.Admin)]
public class AdminApplicationsController(
    AdminApiDbContext dbContext,
    ApplicationStatusService statusService,
    IValidator<StatusChangeRequest> validator) : ApiControllerBase
{
    private const int MaxPageSize = 100;

    [HttpGet]
    [ProducesResponseType(typeof(AdminApplicationListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] ApplicationStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = dbContext.Applications.AsNoTracking().Where(application => status == null || application.Status == status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(application => application.CreatedAt).ThenBy(application => application.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var clientIds = items.Select(item => item.ClientId).ToList();
        var clientTypeMap = await dbContext.Set<OpenIddictEntityFrameworkCoreApplication>()
            .AsNoTracking()
            .Where(app => app.ClientId != null && clientIds.Contains(app.ClientId))
            .ToDictionaryAsync(app => app.ClientId!, app => app.ClientType, cancellationToken);

        return this.Ok(new AdminApplicationListResponse(
            [.. items.Select(item => AdminApplicationResponse.From(item, ToClientType(clientTypeMap.GetValueOrDefault(item.ClientId))))],
            total,
            page,
            pageSize));
    }

    [HttpGet("{applicationId:guid}")]
    [ProducesResponseType(typeof(AdminApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await dbContext.Applications.AsNoTracking().SingleOrDefaultAsync(item => item.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return this.NotFoundProblem();
        }

        var clientTypeStr = await dbContext.Set<OpenIddictEntityFrameworkCoreApplication>()
            .AsNoTracking()
            .Where(app => app.ClientId == application.ClientId)
            .Select(app => app.ClientType)
            .FirstOrDefaultAsync(cancellationToken);

        return this.Ok(AdminApplicationResponse.From(application, ToClientType(clientTypeStr)));
    }

    private static OAuthClientType? ToClientType(string? type) => type switch
    {
        ClientTypes.Public => OAuthClientType.Public,
        ClientTypes.Confidential => OAuthClientType.Confidential,
        _ => null,
    };

    [HttpPut("{applicationId:guid}/status")]
    [ProducesResponseType(typeof(StatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(Guid applicationId, StatusChangeRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return this.ToValidationProblem(validation);
        }

        var clientIp = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (outcome, result) = await statusService.ChangeAsync(
            this.CurrentMemberId(), clientIp, applicationId, request.Status, request.Reason, cancellationToken);

        return outcome switch
        {
            StatusChangeOutcome.NotFound => this.NotFoundProblem(),
            StatusChangeOutcome.Unchanged => this.ConflictProblem($"應用程式已經是 {request.Status} 狀態。"),
            _ => this.Ok(result),
        };
    }
}
