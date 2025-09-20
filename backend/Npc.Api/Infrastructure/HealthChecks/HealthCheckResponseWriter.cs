using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Npc.Api.Infrastructure.HealthChecks
{
    public static class HealthCheckResponseWriter
    {
        public static async Task WriteResponse(HttpContext context, HealthReport healthReport)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = new
            {
                status = healthReport.Status.ToString(),
                totalDuration = healthReport.TotalDuration.TotalMilliseconds,
                checks = healthReport.Entries.Select(x => new
                {
                    name = x.Key,
                    status = x.Value.Status.ToString(),
                    duration = x.Value.Duration.TotalMilliseconds,
                    description = x.Value.Description,
                    tags = x.Value.Tags
                })
            };

            var jsonString = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await context.Response.WriteAsync(jsonString);
        }

        public static async Task WriteDetailedResponse(HttpContext context, HealthReport healthReport)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = new
            {
                status = healthReport.Status.ToString(),
                totalDuration = healthReport.TotalDuration.TotalMilliseconds,
                timestamp = DateTimeOffset.UtcNow,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown",
                checks = healthReport.Entries.Select(x => new
                {
                    name = x.Key,
                    status = x.Value.Status.ToString(),
                    duration = x.Value.Duration.TotalMilliseconds,
                    description = x.Value.Description,
                    tags = x.Value.Tags,
                    data = x.Value.Data,
                    exception = x.Value.Exception?.Message
                }),
                summary = new
                {
                    total = healthReport.Entries.Count,
                    healthy = healthReport.Entries.Count(x => x.Value.Status == HealthStatus.Healthy),
                    degraded = healthReport.Entries.Count(x => x.Value.Status == HealthStatus.Degraded),
                    unhealthy = healthReport.Entries.Count(x => x.Value.Status == HealthStatus.Unhealthy)
                }
            };

            var jsonString = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await context.Response.WriteAsync(jsonString);
        }
    }
}