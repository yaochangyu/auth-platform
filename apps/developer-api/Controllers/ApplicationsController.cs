using System.Security.Claims;
using DeveloperApi.Contracts;
using DeveloperApi.Entities;
using DeveloperApi.Repositories;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DeveloperApi.Controllers;

[ApiController]
[Route("api/v1/applications")]
[Authorize(Policy = AuthPolicies.DeveloperApi)]
public class ApplicationsController(
    ApplicationRepository repository,
    IValidator<ApplicationRequest> validator,
    TimeProvider timeProvider) : ControllerBase
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

        return this.Ok(ApplicationResponse.From(application));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // 會員身分只取自已驗證 Access Token 的 sub claim，請求內容中的任何欄位都不能指定擁有者。
    private Guid CurrentMemberId() => Guid.Parse(this.User.FindFirstValue("sub")!);

    // 不存在與不屬於呼叫者一律回 404，避免洩漏他人專案是否存在。
    private ObjectResult NotFoundProblem() => this.Problem(
        type: "https://auth.1111.com.tw/errors/not-found", title: "找不到指定的應用專案", statusCode: StatusCodes.Status404NotFound);

    private ObjectResult ToValidationProblem(ValidationResult result)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in result.Errors)
        {
            modelState.AddModelError(char.ToLowerInvariant(error.PropertyName[0]) + error.PropertyName[1..], error.ErrorMessage);
        }

        var problem = (ObjectResult)this.ValidationProblem(modelState);
        if (problem.Value is ValidationProblemDetails details)
        {
            details.Title = "請求參數驗證失敗";
            details.Type = "https://auth.1111.com.tw/errors/validation-failed";
        }

        return problem;
    }
}
