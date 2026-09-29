using DeveloperApi.Contracts;
using DeveloperApi.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperApi.Controllers;

[Route("api/v1/applications/{applicationId:guid}/api-keys")]
[Authorize(Policy = AuthPolicies.DeveloperApi)]
public class ApiKeysController(
    ApplicationRepository applications,
    ApiKeyRepository apiKeys,
    IValidator<ApiKeyRequest> validator,
    TimeProvider timeProvider) : DeveloperApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiKeyListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid applicationId, CancellationToken cancellationToken)
    {
        if (await applications.FindAsync(this.CurrentMemberId(), applicationId, cancellationToken) is null)
        {
            return this.NotFoundProblem();
        }

        var now = timeProvider.GetUtcNow();
        var keys = await apiKeys.ListAsync(applicationId, cancellationToken);
        return this.Ok(new ApiKeyListResponse([.. keys.Select(key => ApiKeySummary.From(key, now))], ApiKeyRepository.AllowedScopes));
    }

    [HttpPost]
    [ProducesResponseType(typeof(IssuedApiKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid applicationId, ApiKeyRequest request, CancellationToken cancellationToken)
    {
        if (await applications.FindAsync(this.CurrentMemberId(), applicationId, cancellationToken) is null)
        {
            return this.NotFoundProblem();
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return this.ToValidationProblem(validation);
        }

        var created = await apiKeys.CreateAsync(
            applicationId, request.Name, request.Environment, request.Scopes, timeProvider.GetUtcNow(), request.ExpiresAt, cancellationToken);

        // 明文只回應這一次，禁止任何快取。
        this.Response.Headers.CacheControl = "no-store";
        return this.StatusCode(
            StatusCodes.Status201Created,
            new IssuedApiKeyResponse(
                created.Entity.Id, created.ApiKey, created.ApiSecret, created.Entity.Prefix, created.Entity.Scopes, created.Entity.CreatedAt, created.Entity.ExpiresAt));
    }

    [HttpDelete("{keyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid applicationId, Guid keyId, CancellationToken cancellationToken)
    {
        if (await applications.FindAsync(this.CurrentMemberId(), applicationId, cancellationToken) is null)
        {
            return this.NotFoundProblem();
        }

        var key = await apiKeys.FindAsync(applicationId, keyId, cancellationToken);
        if (key is null || key.RevokedAt is not null)
        {
            return this.NotFoundProblem();
        }

        key.RevokedAt = timeProvider.GetUtcNow();
        await apiKeys.SaveChangesAsync(cancellationToken);
        return this.NoContent();
    }
}
