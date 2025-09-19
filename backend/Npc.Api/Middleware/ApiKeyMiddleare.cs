using Microsoft.Extensions.Options;

namespace Npc.Api.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly PathString[] Excluded =
        [
            new PathString("/swagger"),
            new PathString("/health"),
            new PathString("/health/ready"),
            new PathString("/health/live"),
            new PathString("/metrics")
        ];

        public ApiKeyMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext ctx, IOptions<SecurityOptions> opts)
        {
            if (IsExcluded(ctx.Request.Path))
            {
                await _next(ctx);
                return;
            }

            var keys = opts.Value.ApiKeys;
            if (keys.Length == 0)
            {
                await WriteError(ctx, StatusCodes.Status503ServiceUnavailable, "SecurityNotConfigured");
                return;
            }

            var provided = ctx.Request.Headers["X-API-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(provided) || !keys.Contains(provided))
            {
                await WriteError(ctx, StatusCodes.Status401Unauthorized, "Unauthorized");
                return;
            }

            await _next(ctx);
        }

        private static bool IsExcluded(PathString path) =>
            Excluded.Any(e => path.StartsWithSegments(e, StringComparison.OrdinalIgnoreCase));

        private static async Task WriteError(HttpContext ctx, int status, string code)
        {
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync($"{{\"error\":\"{code}\"}}");
            }
        }
}

    public sealed class SecurityOptions
    {
        public string[] ApiKeys { get; set; } = [];
        public string[] AllowedOrigins { get; set; } = [];
        public RateLimitOptions RateLimit { get; set; } = new();
    }

    public sealed class RateLimitOptions
    {
        public int PermitLimit { get; set; } = 100;
        public int WindowSeconds { get; set; } = 60;
    }
    public static class ApiKeyMiddlewareExtensions
    {
        public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app)
            => app.UseMiddleware<ApiKeyMiddleware>();
    }

}