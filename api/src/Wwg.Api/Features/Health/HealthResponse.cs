using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Wwg.Api.Features.Health;

/// <summary>The API's overall health.</summary>
/// <param name="Status">Healthy or Degraded (200), or Unhealthy (503).</param>
public sealed record HealthResponse(HealthStatus Status);
