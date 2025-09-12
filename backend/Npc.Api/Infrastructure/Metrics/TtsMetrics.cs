using System.Diagnostics.Metrics;

namespace Npc.Api.Infrastructure.Metrics
{
    public sealed class TtsMetrics
    {
        private readonly Counter<long> _requests;
        private readonly Counter<long> _success;
        private readonly Counter<long> _failure;
        private readonly Histogram<double> _durationMs;

        public TtsMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("TTS-Service");
            _requests   = meter.CreateCounter<long>("service.requests", unit: "count", description: "Total agent generation requests");
            _success    = meter.CreateCounter<long>("service.requests.success", unit: "count", description: "Successful agent generation requests");
            _failure    = meter.CreateCounter<long>("service.requests.failure", unit: "count", description: "Failed agent generation requests");
            _durationMs = meter.CreateHistogram<double>("service.request.duration.ms", unit: "ms", description: "Agent request latency");
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