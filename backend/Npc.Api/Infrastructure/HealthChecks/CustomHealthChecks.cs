using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Neo4j.Driver;
using Npc.Api.Infrastructure.Cache;
using Npc.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Npc.Api.Infrastructure.HealthChecks
{
    // Custom health check for application-specific logic
    public class ApplicationHealthCheck : IHealthCheck
    {
        private readonly ILogger<ApplicationHealthCheck> _logger;

        public ApplicationHealthCheck(ILogger<ApplicationHealthCheck> logger)
        {
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check application-specific logic
                var isHealthy = true; // Your application logic here

                if (isHealthy)
                {
                    _logger.LogDebug("Application health check passed");
                    return Task.FromResult(HealthCheckResult.Healthy("Application is running normally"));
                }

                _logger.LogWarning("Application health check failed");
                return Task.FromResult(HealthCheckResult.Unhealthy("Application is not running normally"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application health check threw an exception");
                return Task.FromResult(HealthCheckResult.Unhealthy("Application health check failed", ex));
            }
        }
    }

    // Health check for cache service functionality
    public class CacheHealthCheck : IHealthCheck
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<CacheHealthCheck> _logger;

        public CacheHealthCheck(ICacheService cacheService, ILogger<CacheHealthCheck> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var testKey = "health_check_test";
                var testValue = new { timestamp = DateTimeOffset.UtcNow, test = "cache_health" };

                // Test write operation
                await _cacheService.SetAsync(testKey, testValue, TimeSpan.FromMinutes(1), cancellationToken);

                // Test read operation
                var retrieved = await _cacheService.GetAsync<object>(testKey, cancellationToken);

                // Test delete operation
                await _cacheService.RemoveAsync(testKey, cancellationToken);

                if (retrieved != null)
                {
                    _logger.LogDebug("Cache health check passed");
                    return HealthCheckResult.Healthy("Cache service is working correctly");
                }

                _logger.LogWarning("Cache health check failed - could not retrieve test value");
                return HealthCheckResult.Degraded("Cache service is not working correctly");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache health check threw an exception");
                return HealthCheckResult.Unhealthy("Cache service is unhealthy", ex);
            }
        }
    }

    // Health check for external services
    public class ExternalServicesHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<AgentOptions> _agentOptions;
        private readonly IOptions<TtsOptions> _ttsOptions;
        private readonly IOptions<ImageGenOptions> _imageGenOptions;
        private readonly ILogger<ExternalServicesHealthCheck> _logger;

        public ExternalServicesHealthCheck(
            IHttpClientFactory httpClientFactory,
            IOptions<AgentOptions> agentOptions,
            IOptions<TtsOptions> ttsOptions,
            IOptions<ImageGenOptions> imageGenOptions,
            ILogger<ExternalServicesHealthCheck> logger)
        {
            _httpClientFactory = httpClientFactory;
            _agentOptions = agentOptions;
            _ttsOptions = ttsOptions;
            _imageGenOptions = imageGenOptions;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var results = new List<(string Service, bool IsHealthy, string Message)>();

            // Check Agent Service
            if (!string.IsNullOrWhiteSpace(_agentOptions.Value.BaseUrl))
            {
                var agentHealth = await CheckServiceHealth("Agent", _agentOptions.Value.BaseUrl, cancellationToken);
                results.Add(agentHealth);
            }

            // Check TTS Service
            if (!string.IsNullOrWhiteSpace(_ttsOptions.Value.BaseUrl))
            {
                var ttsHealth = await CheckServiceHealth("TTS", _ttsOptions.Value.BaseUrl, cancellationToken);
                results.Add(ttsHealth);
            }

            // Check Image Generation Service
            if (!string.IsNullOrWhiteSpace(_imageGenOptions.Value.BaseUrl))
            {
                var imageHealth = await CheckServiceHealth("ImageGen", _imageGenOptions.Value.BaseUrl, cancellationToken);
                results.Add(imageHealth);
            }

            var healthyCount = results.Count(r => r.IsHealthy);
            var totalCount = results.Count;

            var data = results.ToDictionary(r => r.Service, r => new { isHealthy = r.IsHealthy, message = r.Message });

            if (healthyCount == totalCount)
            {
                _logger.LogDebug("All external services are healthy ({HealthyCount}/{TotalCount})", healthyCount, totalCount);
                return HealthCheckResult.Healthy($"All external services are healthy ({healthyCount}/{totalCount})", (IReadOnlyDictionary<string, object>?)data);
            }

            if (healthyCount > 0)
            {
                _logger.LogWarning("Some external services are unhealthy ({HealthyCount}/{TotalCount})", healthyCount, totalCount);
                return HealthCheckResult.Degraded($"Some external services are unhealthy ({healthyCount}/{totalCount})", null, (IReadOnlyDictionary<string, object>?)data);
            }

            _logger.LogError("All external services are unhealthy ({HealthyCount}/{TotalCount})", healthyCount, totalCount);
            return HealthCheckResult.Unhealthy($"All external services are unhealthy ({healthyCount}/{totalCount})", null, (IReadOnlyDictionary<string, object>?)data);
        }

        private async Task<(string Service, bool IsHealthy, string Message)> CheckServiceHealth(string serviceName, string baseUrl, CancellationToken cancellationToken)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5); // Short timeout for health checks

                var healthUrl = $"{baseUrl.TrimEnd('/')}/health";
                var response = await httpClient.GetAsync(healthUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return (serviceName, true, "Service is healthy");
                }

                return (serviceName, false, $"Service returned {response.StatusCode}");
            }
            catch (TaskCanceledException)
            {
                return (serviceName, false, "Service health check timed out");
            }
            catch (HttpRequestException ex)
            {
                return (serviceName, false, $"Service connection failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (serviceName, false, $"Service health check error: {ex.Message}");
            }
        }
    }

    // Health check for database operations beyond connection
    public class DatabaseOperationsHealthCheck : IHealthCheck
    {
        private readonly Data.CharacterDbContext _context;
        private readonly IDriver _neo4jDriver;
        private readonly ILogger<DatabaseOperationsHealthCheck> _logger;

        public DatabaseOperationsHealthCheck(
            Data.CharacterDbContext context,
            IDriver neo4jDriver,
            ILogger<DatabaseOperationsHealthCheck> logger)
        {
            _context = context;
            _neo4jDriver = neo4jDriver;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var results = new List<(string Database, bool IsHealthy, string Message, TimeSpan ResponseTime)>();

            // Test PostgreSQL operations
            var pgStart = DateTime.UtcNow;
            try
            {
                var count = await _context.Characters.CountAsync(cancellationToken);
                var pgTime = DateTime.UtcNow - pgStart;
                results.Add(("PostgreSQL", true, $"Query successful, {count} characters", pgTime));
                _logger.LogDebug("PostgreSQL operations health check passed in {ResponseTime}ms", pgTime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                var pgTime = DateTime.UtcNow - pgStart;
                results.Add(("PostgreSQL", false, $"Query failed: {ex.Message}", pgTime));
                _logger.LogError(ex, "PostgreSQL operations health check failed");
            }

            // Test Neo4j operations
            var neo4jStart = DateTime.UtcNow;
            try
            {
                await using var session = _neo4jDriver.AsyncSession();
                var result = await session.ExecuteReadAsync(async tx =>
                {
                    var cursor = await tx.RunAsync("MATCH (n) RETURN count(n) as nodeCount");
                    var record = await cursor.SingleAsync();
                    return record["nodeCount"].As<long>();
                });

                var neo4jTime = DateTime.UtcNow - neo4jStart;
                results.Add(("Neo4j", true, $"Query successful, {result} nodes", neo4jTime));
                _logger.LogDebug("Neo4j operations health check passed in {ResponseTime}ms", neo4jTime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                var neo4jTime = DateTime.UtcNow - neo4jStart;
                results.Add(("Neo4j", false, $"Query failed: {ex.Message}", neo4jTime));
                _logger.LogError(ex, "Neo4j operations health check failed");
            }

            var healthyCount = results.Count(r => r.IsHealthy);
            var totalCount = results.Count;

            var data = results.ToDictionary(r => r.Database, r => new
            {
                isHealthy = r.IsHealthy,
                message = r.Message,
                responseTimeMs = r.ResponseTime.TotalMilliseconds
            });

            if (healthyCount == totalCount)
            {
                return HealthCheckResult.Healthy($"All database operations are healthy ({healthyCount}/{totalCount})", (IReadOnlyDictionary<string, object>?)data);
            }

            if (healthyCount > 0)
            {
                return HealthCheckResult.Degraded($"Some database operations are unhealthy ({healthyCount}/{totalCount})", null, (IReadOnlyDictionary<string, object>?)data);
            }

            return HealthCheckResult.Unhealthy($"All database operations are unhealthy ({healthyCount}/{totalCount})", null, (IReadOnlyDictionary<string, object>?)data);
        }
    }
}