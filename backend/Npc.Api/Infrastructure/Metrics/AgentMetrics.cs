using System.Diagnostics.Metrics;

namespace Npc.Api.Infrastructure.Metrics
{
    public sealed class AgentMetrics
    {
        private readonly Counter<long> _requests;
        private readonly Counter<long> _success;
        private readonly Counter<long> _failure;
        private readonly Histogram<double> _durationMs;

        public AgentMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("Npc.Api.Agent");
            _requests   = meter.CreateCounter<long>("agent.requests", unit: "count", description: "Total agent generation requests");
            _success    = meter.CreateCounter<long>("agent.requests.success", unit: "count", description: "Successful agent generation requests");
            _failure    = meter.CreateCounter<long>("agent.requests.failure", unit: "count", description: "Failed agent generation requests");
            _durationMs = meter.CreateHistogram<double>("agent.request.duration.ms", unit: "ms", description: "Agent request latency");
        }

        public void Record(bool success, double ms)
        {
            _requests.Add(1);
            _durationMs.Record(ms);
            if (success) _success.Add(1);
            else _failure.Add(1);
        }
    }
}