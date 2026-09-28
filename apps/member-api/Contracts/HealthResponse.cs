namespace MemberApi.Contracts;

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
}

public record HealthCheckItem(string Component, HealthStatus Status, double? LatencyMs);

public record HealthResponse(
    HealthStatus Status,
    DateTimeOffset Timestamp,
    string Version,
    IReadOnlyList<HealthCheckItem> Checks);
