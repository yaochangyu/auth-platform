using MemberApi.Contracts;

namespace MemberApi.Handlers;

public interface IHealthCheckHandler
{
    Task<HealthResponse> CheckAsync(CancellationToken cancellationToken);
}
