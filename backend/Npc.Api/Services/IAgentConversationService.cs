using Npc.Api.Dtos;

namespace Npc.Api.Services
{
    public interface IAgentConversationService
    {
        Task<GeneratedUtterance[]> GenerateAsync(Guid conversationId, AutoExpandedRequest req, CancellationToken ct);
        
        Task<LoreSuggestedItem[]> GenerateLoreAsync(LoreSuggestRequest req, CancellationToken ct);
    }
}