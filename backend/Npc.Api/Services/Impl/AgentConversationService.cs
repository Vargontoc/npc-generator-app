using System.Diagnostics;
using Microsoft.Extensions.Options;
using Npc.Api.Dtos;
using Npc.Api.Infrastructure.Metrics;

namespace Npc.Api.Services.Impl
{
    public class AgentConversationService(HttpClient client, IOptions<AgentOptions> opts, AgentMetrics metrics, ILogger<AgentConversationService> logger) : IAgentConversationService
    {
        public async Task<GeneratedUtterance[]> GenerateAsync(Guid conversationId, AutoExpandedRequest req, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var success = false;
            try
            {
                    var payload = new AutoExpandedRequest(conversationId, req.Count, req.Context, req.FromUtteranceId);
                    using var msg = new HttpRequestMessage(HttpMethod.Post, "");
                    msg.Content = JsonContent.Create(payload);
                    if (!string.IsNullOrEmpty(opts.Value.ApiKey))
                        msg.Headers.TryAddWithoutValidation("X-API-Key", opts.Value.ApiKey);

                    using var resp = await client.SendAsync(msg, ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        logger.LogWarning("Agent call failed {Status} for conversation {ConversationId}", (int)resp.StatusCode, conversationId);
                        return [];
                    }

                    var dto = await resp.Content.ReadFromJsonAsync<AgentGeneratedResponse>(cancellationToken:ct);
                    if (dto?.Items == null || dto.Items.Length == 0)
                    {
                        logger.LogInformation("Agent returned no items for conversation {ConversationId}", conversationId);
                        success = true; 
                        return [];
                    }

                success = true;
                    return [.. dto.Items.Select(i => new GeneratedUtterance(i.Text, i.CharacterId, i.Tags))];
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.LogDebug("Agent request cancelled for conversation {ConversationId}", conversationId);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Agent request exception for conversation {ConversationId}", conversationId);
                return Array.Empty<GeneratedUtterance>();
            }
            finally
            {
                sw.Stop();
                metrics.Record(success, sw.Elapsed.TotalMilliseconds);
            }



        }
    }
}