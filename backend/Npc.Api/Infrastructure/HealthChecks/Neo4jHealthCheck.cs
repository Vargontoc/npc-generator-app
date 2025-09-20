using Microsoft.Extensions.Diagnostics.HealthChecks;
using Neo4j.Driver;

namespace Npc.Api.Infrastructure.HealthChecks
{
    public class Neo4jHealthCheck : IHealthCheck
    {
        private readonly IDriver _driver;
        private readonly ILogger<Neo4jHealthCheck> _logger;

        public Neo4jHealthCheck(IDriver driver, ILogger<Neo4jHealthCheck> logger)
        {
            _driver = driver;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;

                await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));

                // Simple connectivity test
                var result = await session.ExecuteReadAsync(async tx =>
                {
                    var cursor = await tx.RunAsync("RETURN 1 as result");
                    var record = await cursor.SingleAsync();
                    return record["result"].As<long>();
                });

                var duration = DateTime.UtcNow - startTime;

                if (result == 1)
                {
                    _logger.LogDebug("Neo4j health check passed in {Duration}ms", duration.TotalMilliseconds);
                    return HealthCheckResult.Healthy(
                        "Neo4j connection is healthy",
                        new Dictionary<string, object>
                        {
                            ["responseTime"] = duration.TotalMilliseconds,
                            ["server"] = "connected"
                        });
                }

                _logger.LogWarning("Neo4j health check failed - unexpected result: {Result}", result);
                return HealthCheckResult.Unhealthy("Neo4j returned unexpected result");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Neo4j health check failed");
                return HealthCheckResult.Unhealthy("Neo4j connection failed", ex);
            }
        }
    }
}