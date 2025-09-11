namespace Npc.Api.Dtos
{
    public record ConversationCreateRequest(string Title);
    public record UtteranceCreateRequest(string Text, Guid? CharacterId);
    public record BranchCreateRequest(Guid FromUtteranceId, Guid ToUtteranceId);
    public record ConversationResponse(Guid Id, string Title);
    public record UtteranceResponse(Guid Id, string Text, Guid? CharacterId);
    public record PathResponse(Guid ConversationId, string Title, UtteranceResponse[] Path);
}