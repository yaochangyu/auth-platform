using System.Diagnostics;
using System.Reflection;
using MemberApi.Contracts;
using MemberApi.Infrastructure.Persistence;

namespace MemberApi.Handlers;

public class HealthCheckHandler(MemberApiDbContext dbContext, TimeProvider timeProvider) : IHealthCheckHandler
{
    private static readonly string ApiVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public async Task<HealthResponse> CheckAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var isDatabaseHealthy = await dbContext.Database.CanConnectAsync(cancellationToken);
        stopwatch.Stop();

        var databaseCheck = new HealthCheckItem(
            "PostgreSQL",
            isDatabaseHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            stopwatch.Elapsed.TotalMilliseconds);

        var overallStatus = isDatabaseHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy;

        return new HealthResponse(
            overallStatus,
            timeProvider.GetUtcNow(),
            ApiVersion,
            [databaseCheck]);
    }
}
