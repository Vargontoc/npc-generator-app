using System.Diagnostics;
using Microsoft.Extensions.Options;
using Npc.Api.Dtos;

namespace Npc.Api.Services.Impl
{
    public class AgentConversationService(HttpClient client, IOptions<AgentOptions> opts) : IAgentConversationService
    {
        public async Task<GeneratedUtterance[]> GenerateAsync(Guid conversationId, AutoExpandedRequest req, CancellationToken ct)
        {

            var payload = new AutoExpandedRequest(conversationId, req.Count, req.Context, req.FromUtteranceId);
            using var msg = new HttpRequestMessage(HttpMethod.Post, "");
            msg.Content = JsonContent.Create(payload);
            if (!string.IsNullOrEmpty(opts.Value.ApiKey))
                msg.Headers.TryAddWithoutValidation("X-API-Key", opts.Value.ApiKey);

            using var resp = await client.SendAsync(msg, ct);
            if (!resp.IsSuccessStatusCode)
                return [];

            var dto = await resp.Content.ReadFromJsonAsync<AgentGeneratedResponse>(cancellationToken:ct);
            if (dto?.Items == null || dto.Items.Length == 0)
                return [];


            return [.. dto.Items.Select(i => new GeneratedUtterance(i.Text, i.CharacterId, i.Tags))];
        }
    }
}