using System.Text.Json;
using Microsoft.Extensions.Options;
using Npc.Api.Dtos;
using Npc.Api.Infrastructure.Metrics;

namespace Npc.Api.Services.Impl
{
    public class ImageGenService(HttpClient client, IOptions<ImageGenOptions> opts, ImageGenMetrics metrics, ILogger<ImageGenService> logger) : IImageGenService
    {
         private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
        public async Task<ImageJobAccepted> GenerateAsync(ImageRequest req, CancellationToken ct)
        {
            EnsureConfigured();
            var payload = new
            {
                prompt = req.Prompt,
                negative_prompt = req.Negative_Prompt,
                @params = new
                {
                    width = req.Width,
                    height = req.Height,
                    steps = req.Steps,
                    cfg = req.Cfg,
                    seed = req.Seed,
                    model = req.Model
                },
                safety = new { allow_ature_implicit = false },
                metada = new { project_id = (string?)null, agent_id = (string?)null }
            };

            using var response = await client.PostAsJsonAsync("v1/generate", payload, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
            {
                var txt = await response.Content.ReadAsStringAsync(ct);
                throw new ImageGenException($"Generate failed {response.StatusCode}: {txt}", (int)response.StatusCode);
            }

            var accepted = await response.Content.ReadFromJsonAsync<ImageJobAccepted>(JsonOpts, ct)
                        ?? throw new ImageGenException("Empty upstream response", 502);

            return accepted;
        }

        public async Task<ImageJobStatus> GetJobAsync(string jobId, CancellationToken ct)
        {
            EnsureConfigured();
            using var resp = await client.GetAsync($"v1/jobs/{jobId}", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new ImageGenException("Job not found", 404);
            if (!resp.IsSuccessStatusCode)
                throw new ImageGenException($"Upstream error {resp.StatusCode}", (int)resp.StatusCode);

            var status = await resp.Content.ReadFromJsonAsync<ImageJobStatus>(JsonOpts, ct)
                        ?? throw new ImageGenException("Invalid upstream job payload", 502);
            return status;
        }

        public async Task<string> GetModelsRawAsync(CancellationToken ct)
        {
            EnsureConfigured();
            using var resp = await client.GetAsync("v1/models", ct);
            if (!resp.IsSuccessStatusCode)
                throw new ImageGenException($"Upstream error {resp.StatusCode}", (int)resp.StatusCode);
            return await resp.Content.ReadAsStringAsync(ct);
        }



        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(opts.Value.BaseUrl))
                throw new ImageGenException("Image service not configured", 503);
        }

        public class ImageGenException : Exception
        {
            public int StatusCode { get; }
            public ImageGenException(string message, int statusCode) : base(message) => StatusCode = statusCode;
        }
    }
}

