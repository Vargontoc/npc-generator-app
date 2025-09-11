using Npc.Api.Dtos;

namespace Npc.Api.Services.Impl
{
    public class AgentConversationService : IAgentConversationService
    {
        public Task<GeneratedUtterance[]> GenerateAsync(Guid conversationId, AutoExpandedRequest req, CancellationToken ct)
        {
            var count = req.Count <= 0 ? 1 : Math.Min(req.Count, 5);
            var list = Enumerable.Range(1, count)
                .Select(i => new GeneratedUtterance($"[AutoGen {i}] Placeholder line", null, Array.Empty<string>()))
                .ToArray();
            return Task.FromResult(list);
        }
    }
}