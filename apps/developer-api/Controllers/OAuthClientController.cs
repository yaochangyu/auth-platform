using AuthShared.Web;
using DeveloperApi.Contracts;
using DeveloperApi.Entities;
using DeveloperApi.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace DeveloperApi.Controllers;

[Route("api/v1/applications/{applicationId:guid}/oauth-client")]
[Authorize(Policy = AuthPolicies.DeveloperApi)]
public class OAuthClientController(
    ApplicationRepository applications,
    OAuthClientRepository oauthClients,
    IValidator<OAuthClientRequest> validator,
    TimeProvider timeProvider) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(OAuthClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid applicationId, CancellationToken cancellationToken)
    {
        var project = await applications.FindAsync(this.CurrentMemberId(), applicationId, cancellationToken);
        return project is null ? this.NotFoundProblem() : this.Ok(await this.BuildResponseAsync(project, cancellationToken));
    }

    [HttpPut]
    [ProducesResponseType(typeof(OAuthClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Put(Guid applicationId, OAuthClientRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return this.ToValidationProblem(validation);
        }

        var project = await applications.FindAsync(this.CurrentMemberId(), applicationId, cancellationToken);
        if (project is null)
        {
            return this.NotFoundProblem();
        }

        return await this.HandleConcurrencyAsync(async () =>
        {
            await oauthClients.SaveConfigAsync(project, request, cancellationToken);
            return this.Ok(await this.BuildResponseAsync(project, cancellationToken));
        });
    }

    [HttpPost("secrets")]
    [ProducesResponseType(typeof(IssuedClientSecretResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IssueSecret(Guid applicationId, CancellationToken cancellationToken)
    {
        var project = await applications.FindAsync(this.CurrentMemberId(), applicationId, cancellationToken);
        if (project is null)
        {
            return this.NotFoundProblem();
        }

        return await this.HandleConcurrencyAsync(async () =>
        {
            var (outcome, plaintext, entry) = await oauthClients.IssueSecretAsync(project.ClientId, cancellationToken);
            switch (outcome)
            {
                case IssueSecretOutcome.NotConfidential:
                    return this.ConflictProblem("只有 Confidential Client 可以發行 Client Secret，請先將客戶端類型改為 Confidential。");
                case IssueSecretOutcome.LimitReached:
                    return this.ConflictProblem("同時有效的 Secret 已達 2 組，請先作廢舊的 Secret，或等待過渡期結束。");
            }

            // 明文只回應這一次，禁止任何快取。
            this.Response.Headers.CacheControl = "no-store";
            return this.StatusCode(
                StatusCodes.Status201Created,
                new IssuedClientSecretResponse(entry!.Id, plaintext!, entry.Prefix, entry.CreatedAt, entry.ExpiresAt));
        });
    }

    [HttpDelete("secrets/{secretId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSecret(Guid applicationId, Guid secretId, CancellationToken cancellationToken)
    {
        var project = await applications.FindAsync(this.CurrentMemberId(), applicationId, cancellationToken);
        if (project is null)
        {
            return this.NotFoundProblem();
        }

        return await this.HandleConcurrencyAsync(async () =>
            await oauthClients.RevokeSecretAsync(project.ClientId, secretId, cancellationToken) ? this.NoContent() : this.NotFoundProblem());
    }

    private async Task<OAuthClientResponse> BuildResponseAsync(DeveloperApplication project, CancellationToken cancellationToken)
    {
        var state = await oauthClients.GetAsync(project.ClientId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        return new OAuthClientResponse(
            project.ClientId,
            state.ClientType,
            state.RedirectUris,
            state.PostLogoutRedirectUris,
            state.Scopes,
            OAuthClientRepository.AllowedScopes,
            [.. state.Secrets.Entries.Select(entry => new ClientSecretSummary(
                entry.Id,
                entry.Prefix,
                entry.CreatedAt,
                entry.ExpiresAt,
                entry.RevokedAt,
                entry.RevokedAt is not null ? "Revoked"
                    : entry.ExpiresAt is null ? "Active"
                    : entry.ExpiresAt > now ? "Expiring" : "Expired"))]);
    }

    // 兩個請求同時修改同一個 Client 時，OpenIddict 以並行權杖擋下後到的，回 409 讓呼叫端重試。
    private async Task<IActionResult> HandleConcurrencyAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (OpenIddictExceptions.ConcurrencyException)
        {
            return this.ConflictProblem("Client 設定同時被其他請求修改，請重新載入後再試。");
        }
    }
}
