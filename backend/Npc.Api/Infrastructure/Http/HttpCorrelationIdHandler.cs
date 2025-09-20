using Npc.Api.Infrastructure.Middleware;

namespace Npc.Api.Infrastructure.Http
{
    /// <summary>
    /// HTTP message handler to add correlation ID to outgoing HTTP requests
    /// </summary>
    public class HttpCorrelationIdHandler : DelegatingHandler
    {
        private readonly ICorrelationIdService _correlationIdService;
        private readonly ILogger<HttpCorrelationIdHandler> _logger;
        private const string CorrelationIdHeaderName = "X-Correlation-ID";

        public HttpCorrelationIdHandler(ICorrelationIdService correlationIdService, ILogger<HttpCorrelationIdHandler> logger)
        {
            _correlationIdService = correlationIdService;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var correlationId = _correlationIdService.GetCorrelationId();

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                // Add correlation ID to outgoing request headers
                request.Headers.TryAddWithoutValidation(CorrelationIdHeaderName, correlationId);

                _logger.LogDebug("Added correlation ID {CorrelationId} to outgoing request to {RequestUri}",
                    correlationId, request.RequestUri);
            }

            var response = await base.SendAsync(request, cancellationToken);

            // Log response correlation ID if present
            if (response.Headers.TryGetValues(CorrelationIdHeaderName, out var responseCorrelationIds))
            {
                var responseCorrelationId = responseCorrelationIds.FirstOrDefault();
                _logger.LogDebug("Received correlation ID {ResponseCorrelationId} from {RequestUri}",
                    responseCorrelationId, request.RequestUri);
            }

            return response;
        }
    }
}