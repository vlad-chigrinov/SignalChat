using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SignalChat.Infrastructure;

namespace SignalChat.Api.Health;

public static class HealthEndpoints
{
    /// <summary>
    /// <c>/health</c> — liveness: the process answers, no dependency is probed.
    /// <c>/health/ready</c> — readiness: PostgreSQL is reachable.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(DependencyInjection.ReadinessTag),
        });
        return endpoints;
    }
}
