using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Npc.Api.Dtos;
using Npc.Api.Infrastructure.Metrics;

namespace Npc.Api.Services.Impl
{
    public class AgentConversationService(HttpClient client, IOptions<AgentOptions> opts, AgentMetrics metrics, ILogger<AgentConversationService> logger) : IAgentConversationService
    {
         private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public async Task<GeneratedUtterance[]> GenerateAsync(Guid conversationId, AutoExpandedRequest req, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var success = false;

            try
            {
                EnsureConfigured();
                if (req.Count <= 0 || req.Count > 20)
                {
                    logger.LogWarning("Agent request count {Count} out of bounds for conversation {ConversationId}", req.Count, conversationId);
                    return Array.Empty<GeneratedUtterance>();
                }

                var payload = new AutoExpandedRequest(
                    conversationId,
                    req.Count,
                    req.Context,
                    req.FromUtteranceId
                );

                Infrastructure.Observability.Telemetry.AgentCalls.Add(1);
                // Ajusta la ruta si tu agente expone algo distinto:
                var relative = $"conversations/{conversationId}/generate";
                using var msg = new HttpRequestMessage(HttpMethod.Post, relative)
                {
                    Content = JsonContent.Create(payload, options: JsonOpts)
                };


                using var resp = await client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);

                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    logger.LogError("Agent unauthorized for conversation {ConversationId}", conversationId);
                    return [];
                }

                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogWarning("Agent call failed {Status} for conversation {ConversationId}", (int)resp.StatusCode, conversationId);
                    return [];
                }

                var dto = await resp.Content.ReadFromJsonAsync<AgentGeneratedResponse>(JsonOpts, ct);
                if (dto?.Items == null || dto.Items.Length == 0)
                {
                    logger.LogInformation("Agent returned no items for conversation {ConversationId}", conversationId);
                    success = true;
                    return [];
                }

                success = true;
                return [.. dto.Items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Text))
                    .Select(i => new GeneratedUtterance(i.Text!, i.CharacterId, i.Tags))];
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.LogDebug("Agent request cancelled for conversation {ConversationId}", conversationId);
                throw;
            }
            catch (JsonException jx)
            {
                logger.LogError(jx, "Agent JSON parse error for conversation {ConversationId}", conversationId);
                return [];
            }
            catch (Exception ex)
            {
                Infrastructure.Observability.Telemetry.AgentCallFailures.Add(1);

                logger.LogError(ex, "Agent request exception for conversation {ConversationId}", conversationId);
                return [];
            }
            finally
            {
                sw.Stop();
                Infrastructure.Observability.Telemetry.AgentCallDurationMs.Record(sw.Elapsed.TotalMilliseconds);
                metrics.Record(success, sw.Elapsed.TotalMilliseconds);
            }


        }

        public async Task<LoreSuggestedItem[]> GenerateLoreAsync(LoreSuggestRequest req, CancellationToken ct)
        {   
            var sw = Stopwatch.StartNew();
            var success = false;
            try
            {
                EnsureConfigured();

                var relative = "lore/generate";
                var payload = new
                {
                    prompt = req.Prompt,
                    worldId = req.WorldId,
                    count = req.Count
                };

                using var msg = new HttpRequestMessage(HttpMethod.Post, relative)
                {
                    Content = JsonContent.Create(payload, options: JsonOpts)
                };


                using var resp = await client.SendAsync(msg, ct);
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    return Array.Empty<LoreSuggestedItem>();

                if (!resp.IsSuccessStatusCode)
                    return Array.Empty<LoreSuggestedItem>();

                var dto = await resp.Content.ReadFromJsonAsync<RemoteLoreResponse>(JsonOpts, ct);
                if (dto?.Items is null) return Array.Empty<LoreSuggestedItem>();

                return dto.Items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Title) && !string.IsNullOrWhiteSpace(i.Text))
                    .Select(i => new LoreSuggestedItem(i.Title!, i.Text!, i.Model))
                    .ToArray();
                    }
            catch (JsonException jx)
            {
                logger.LogError(jx, "Agent JSON parse error for conversation {WorldId}", req.WorldId);
                return [];
            }
            catch (Exception ex)
            {
                Infrastructure.Observability.Telemetry.AgentCallFailures.Add(1);

                logger.LogError(ex, "Agent request exception for conversation {WorldId}", req.WorldId);
                return [];
            }
            finally
            {
                sw.Stop();
                Infrastructure.Observability.Telemetry.AgentCallDurationMs.Record(sw.Elapsed.TotalMilliseconds);
                metrics.Record(success, sw.Elapsed.TotalMilliseconds);
            }
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(opts.Value.BaseUrl))
                throw new AgentException("Image service not configured", 503);
        }
        private sealed record RemoteLoreItem(string? Title, string? Text, string? Model);
        private sealed record RemoteLoreResponse(RemoteLoreItem[] Items);

    }
    
    public class AgentException : Exception
    {
        public int StatusCode { get; }
        public AgentException(string message, int statusCode) : base(message) => StatusCode = statusCode;
    }
}