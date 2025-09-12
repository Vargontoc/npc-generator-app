namespace Npc.Api.Dtos
{
    public record AutoExpandedRequest(Guid  ConversationId, int Count, string? Context, Guid? FromUtteranceId);
    public record GeneratedUtterance(string Text, Guid? CharacterId, string[]? Tags);
    public record AgentGeneratedResponse(GeneratedUtterance[] Items);

}