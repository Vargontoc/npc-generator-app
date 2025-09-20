using Serilog.Context;

namespace Npc.Api.Infrastructure.Middleware
{
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;
        private const string CorrelationIdHeaderName = "X-Correlation-ID";
        private const string CorrelationIdKey = "CorrelationId";

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Get or generate correlation ID
            var correlationId = GetOrGenerateCorrelationId(context);

            // Add to response headers
            context.Response.Headers.TryAdd(CorrelationIdHeaderName, correlationId);

            // Add to HttpContext items for access throughout the request
            context.Items[CorrelationIdKey] = correlationId;

            // Add to Serilog logging context
            using (LogContext.PushProperty(CorrelationIdKey, correlationId))
            {
                _logger.LogDebug("Request started with correlation ID: {CorrelationId}", correlationId);

                try
                {
                    await _next(context);
                }
                finally
                {
                    _logger.LogDebug("Request completed with correlation ID: {CorrelationId}", correlationId);
                }
            }
        }

        private static string GetOrGenerateCorrelationId(HttpContext context)
        {
            // Check if correlation ID is provided in request headers
            if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var headerValue) &&
                !string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue.ToString();
            }

            // Check if there's a trace ID from Activity (OpenTelemetry)
            if (System.Diagnostics.Activity.Current?.Id is not null)
            {
                return System.Diagnostics.Activity.Current.Id;
            }

            // Generate new correlation ID
            return Guid.NewGuid().ToString("D");
        }
    }

    // Extension method for easy registration
    public static class CorrelationIdMiddlewareExtensions
    {
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CorrelationIdMiddleware>();
        }
    }

    // Service to access correlation ID from anywhere in the application
    public interface ICorrelationIdService
    {
        string? GetCorrelationId();
    }

    public class CorrelationIdService : ICorrelationIdService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CorrelationIdService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? GetCorrelationId()
        {
            return _httpContextAccessor.HttpContext?.Items["CorrelationId"] as string;
        }
    }
}