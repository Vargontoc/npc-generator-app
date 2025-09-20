using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Npc.Api.Infrastructure.Observability;

namespace Npc.Api.Infrastructure.Exceptions
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            _logger.LogError(exception,
                "Exception occurred: {Message} | TraceId: {TraceId}",
                exception.Message,
                traceId);

            // Increment error metrics
            Telemetry.ErrorsTotal.Add(1, new KeyValuePair<string, object?>("exception_type", exception.GetType().Name));

            var (statusCode, title, detail) = GetErrorDetails(exception);

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
                Extensions = { ["traceId"] = traceId }
            };

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/problem+json";

            var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await httpContext.Response.WriteAsync(json, cancellationToken);

            return true;
        }

        private static (int StatusCode, string Title, string Detail) GetErrorDetails(Exception exception)
        {
            return exception switch
            {
                // Business Exceptions
                EntityNotFoundException => (
                    StatusCode: (int)HttpStatusCode.NotFound,
                    Title: "Resource Not Found",
                    Detail: exception.Message
                ),
                ValidationException => (
                    StatusCode: (int)HttpStatusCode.BadRequest,
                    Title: "Validation Error",
                    Detail: exception.Message
                ),
                ExternalServiceException => (
                    StatusCode: (int)HttpStatusCode.BadGateway,
                    Title: "External Service Error",
                    Detail: exception.Message
                ),
                ModerationException => (
                    StatusCode: (int)HttpStatusCode.BadRequest,
                    Title: "Content Moderation Failed",
                    Detail: exception.Message
                ),
                RateLimitException rateLimitEx => (
                    StatusCode: (int)HttpStatusCode.TooManyRequests,
                    Title: "Rate Limit Exceeded",
                    Detail: $"{exception.Message} Retry after {rateLimitEx.RetryAfter.TotalSeconds} seconds"
                ),

                // Framework Exceptions
                ArgumentException => (
                    StatusCode: (int)HttpStatusCode.BadRequest,
                    Title: "Bad Request",
                    Detail: exception.Message
                ),
                InvalidOperationException when exception.Message.Contains("not found") => (
                    StatusCode: (int)HttpStatusCode.NotFound,
                    Title: "Resource Not Found",
                    Detail: exception.Message
                ),
                InvalidOperationException when exception.Message.Contains("World") => (
                    StatusCode: (int)HttpStatusCode.BadRequest,
                    Title: "Invalid World Reference",
                    Detail: exception.Message
                ),
                UnauthorizedAccessException => (
                    StatusCode: (int)HttpStatusCode.Unauthorized,
                    Title: "Unauthorized",
                    Detail: "Access denied"
                ),
                TimeoutException => (
                    StatusCode: (int)HttpStatusCode.RequestTimeout,
                    Title: "Request Timeout",
                    Detail: "The request timed out"
                ),
                HttpRequestException httpEx when httpEx.Message.Contains("timeout") => (
                    StatusCode: (int)HttpStatusCode.RequestTimeout,
                    Title: "External Service Timeout",
                    Detail: "External service request timed out"
                ),
                HttpRequestException => (
                    StatusCode: (int)HttpStatusCode.BadGateway,
                    Title: "External Service Error",
                    Detail: "Error communicating with external service"
                ),
                TaskCanceledException => (
                    StatusCode: (int)HttpStatusCode.RequestTimeout,
                    Title: "Request Cancelled",
                    Detail: "The request was cancelled"
                ),
                _ => (
                    StatusCode: (int)HttpStatusCode.InternalServerError,
                    Title: "Internal Server Error",
                    Detail: "An unexpected error occurred"
                )
            };
        }
    }
}