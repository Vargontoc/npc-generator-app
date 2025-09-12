using System.Diagnostics.Metrics;

namespace Npc.Api.Infrastructure.Observability 
{
    public static class Telemetry
    {
        public const string MeterName = "Npc.Api";
        private static readonly Meter Meter = new(MeterName);

        // Counters
        public static readonly Counter<long> CharactersCreated = Meter.CreateCounter<long>("characters_created");
        public static readonly Counter<long> AgentCalls = Meter.CreateCounter<long>("agent_calls_total");
        public static readonly Counter<long> AgentCallFailures = Meter.CreateCounter<long>("agent_calls_failed_total");
        public static readonly Counter<long> TtsRequests = Meter.CreateCounter<long>("tts_requests_total");
        public static readonly Counter<long> TtsErrors = Meter.CreateCounter<long>("tts_errors_total");
        public static readonly Counter<long> ImageJobsRequested = Meter.CreateCounter<long>("image_jobs_requested_total");
        public static readonly Counter<long> ImageJobsCompleted = Meter.CreateCounter<long>("image_jobs_completed_total");

        // Histograms (ms)
        public static readonly Histogram<double> AgentCallDurationMs = Meter.CreateHistogram<double>("agent_call_duration_ms");
        public static readonly Histogram<double> TtsLatencyMs = Meter.CreateHistogram<double>("tts_latency_ms");
        public static readonly Histogram<double> ImageGenLatencyMs = Meter.CreateHistogram<double>("image_gen_latency_ms");
    }
}