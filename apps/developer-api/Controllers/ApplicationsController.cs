using AuthShared;
using AuthShared.Web;
using DeveloperApi.Contracts;
using DeveloperApi.Entities;
using DeveloperApi.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperApi.Controllers;

[Route("api/v1/applications")]
[Authorize(Policy = AuthPolicies.DeveloperApi)]
public class ApplicationsController(
    ApplicationRepository repository,
    OAuthClientRepository oauthClients,
    IValidator<ApplicationRequest> validator,
    TimeProvider timeProvider) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApplicationListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var applications = await repository.ListAsync(this.CurrentMemberId(), cancellationToken);
        return this.Ok(new ApplicationListResponse(applications.Select(ApplicationResponse.From).ToList()));
    }

    [HttpGet("{applicationId:guid}")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await repository.FindAsync(this.CurrentMemberId(), applicationId, cancellationToken);
        return application is null ? this.NotFoundProblem() : this.Ok(ApplicationResponse.From(application));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(ApplicationRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return this.ToValidationProblem(validation);
        }

        var now = timeProvider.GetUtcNow();
        var application = new DeveloperApplication
        {
            Id = Guid.NewGuid(),
            OwnerMemberId = this.CurrentMemberId(),
            ClientId = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Description = request.Description,
            ContactEmail = request.ContactEmail,
            LogoUrl = NullIfEmpty(request.LogoUrl),
            HomepageUrl = NullIfEmpty(request.HomepageUrl),
            Status = ApplicationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        repository.Add(application);
        await repository.SaveChangesAsync(cancellationToken);

        return this.CreatedAtAction(nameof(this.Get), new { applicationId = application.Id }, ApplicationResponse.From(application));
    }

    [HttpPut("{applicationId:guid}")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid applicationId, ApplicationRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return this.ToValidationProblem(validation);
        }

        var application = await repository.FindAsync(this.CurrentMemberId(), applicationId, cancellationToken);
        if (application is null)
        {
            return this.NotFoundProblem();
        }

        application.Name = request.Name;
        application.Description = request.Description;
        application.ContactEmail = request.ContactEmail;
        application.LogoUrl = NullIfEmpty(request.LogoUrl);
        application.HomepageUrl = NullIfEmpty(request.HomepageUrl);
        application.UpdatedAt = timeProvider.GetUtcNow();
        await repository.SaveChangesAsync(cancellationToken);
        await oauthClients.RenameAsync(application.ClientId, application.Name, cancellationToken);

        return this.Ok(ApplicationResponse.From(application));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
