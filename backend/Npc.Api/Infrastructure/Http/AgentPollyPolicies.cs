using System.Net;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace Npc.Api.Infrastructure.Http
{
    public static class AgentPollyPolicies
    {
        private static readonly Random Jitter = new();

        public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(int maxRetries = 3)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // 5xx + network
                .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests || r.StatusCode == HttpStatusCode.RequestTimeout)
                .WaitAndRetryAsync(
                    maxRetries,
                    attempt =>
                    {
                        var baseDelay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));
                        var jitterMs = Jitter.Next(50, 250);
                        return baseDelay + TimeSpan.FromMilliseconds(jitterMs);
                    });
        }

        public static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30));
        }

        public static IAsyncPolicy<HttpResponseMessage> CreateTimeoutPolicy(int seconds = 5)
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(seconds), TimeoutStrategy.Optimistic);
        }

        public static IAsyncPolicy<HttpResponseMessage> CreateComposite()
        {
            var timeout = CreateTimeoutPolicy();
            var retry = CreateRetryPolicy();
            var breaker = CreateCircuitBreakerPolicy();
            return Policy.WrapAsync(breaker, retry, timeout);
        }
    }
    
}