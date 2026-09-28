using System.Diagnostics;
using System.Reflection;
using MemberApi.Contracts;
using MemberApi.Repositories;

namespace MemberApi.Handlers;

public class HealthCheckHandler(IHealthRepository healthRepository, TimeProvider timeProvider) : IHealthCheckHandler
{
    private static readonly string ApiVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public async Task<HealthResponse> CheckAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var isDatabaseHealthy = await healthRepository.CanConnectAsync(cancellationToken);
        stopwatch.Stop();

        var databaseCheck = new HealthCheckItem(
            "PostgreSQL",
            isDatabaseHealthy ? HealthCheckStatus.Healthy : HealthCheckStatus.Unhealthy,
            stopwatch.Elapsed.TotalMilliseconds);

        var overallStatus = isDatabaseHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy;

        return new HealthResponse(
            overallStatus,
            timeProvider.GetUtcNow(),
            ApiVersion,
            [databaseCheck]);
    }
}
