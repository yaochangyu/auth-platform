namespace MemberApi.Contracts;

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
}

public enum HealthCheckStatus
{
    Healthy,
    Unhealthy,
}

public record HealthCheckItem(string Component, HealthCheckStatus Status, double? LatencyMs);

public record HealthResponse(
    HealthStatus Status,
    DateTimeOffset Timestamp,
    string Version,
    IReadOnlyList<HealthCheckItem> Checks);
