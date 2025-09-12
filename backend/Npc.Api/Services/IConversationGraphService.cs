using Npc.Api.Dtos;

namespace Npc.Api.Services
{
    public interface IConversationGraphService
    {
        Task<ConversationResponse> CreateConversationAsync(string title, CancellationToken ct);
        Task<UtteranceResponse> AddRootUtteranceAsync(Guid conversationId, string text, Guid? characterId, CancellationToken ct);
        Task<UtteranceResponse> AddNextUtterance(Guid fromUtteranceId, string text, Guid? characterId, CancellationToken ct);
        Task AddBranchAsync(Guid fromUtteranceId, Guid toUtteranceId, CancellationToken ct);
        Task<PathResponse?> GetLinearPathAsync(Guid conversationId, CancellationToken ct);
        Task<UtteranceDetail?> GetUtteranceAsync(Guid utteranceId, CancellationToken ct);
        Task<UtteranceDetail?> UpdateUtteranceAsync(Guid utteranceId, string text, string[]? tags, int expectedVersion, CancellationToken ct);
        Task<bool> SoftDeleteUtteranceAsync(Guid utteranceId, CancellationToken ct);
        Task<GraphResponse?> GetGraphAsync(Guid conversationId, int depth, CancellationToken ct);
        Task<PathResponse?> GetRandomPathAsync(Guid conversationId, int maxDepth, CancellationToken ct);
        Task SetBranchWeightAsync(Guid fromUtteranceId, Guid toUtteranceId, double? weight, CancellationToken ct);
        Task<ConversationResponse> ImportConversationAsync(ConversationImportRequest req, CancellationToken ct);
    Task<ConversationExportResponse?> ExportConversationAsync(Guid conversationId, int depth, CancellationToken ct);
        Task<GeneratedUtterance[]> AutoExpandedAsync(Guid conversationId, AutoExpandedRequest req, CancellationToken ct);
    }
}