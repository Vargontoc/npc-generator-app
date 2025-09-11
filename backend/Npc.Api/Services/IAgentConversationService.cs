using Npc.Api.Dtos;

namespace Npc.Api.Services
{
    public interface IAgentConversationService
    {
        Task<GeneratedUtterance[]> GenerateAsync(Guid conversationId, AutoExpandedRequest req, CancellationToken ct);
        
    }
}