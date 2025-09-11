namespace Npc.Api.Dtos
{
    public record ConversationCreateRequest(string Title);
    public record UtteranceCreateRequest(string Text, Guid? CharacterId);
   public record UtteranceUpdateRequest(string Text, Guid? CharacterId, int Version, string[]? Tags); 
    public record BranchCreateRequest(Guid FromUtteranceId, Guid ToUtteranceId, double? Weight);
    public record ConversationResponse(Guid Id, string Title);
    public record UtteranceResponse(Guid Id, string Text, Guid? CharacterId);
    public record PathResponse(Guid ConversationId, string Title, UtteranceResponse[] Path);
    public record UtteranceDetail(Guid Id, string Text, Guid? CharacterId, bool Deleted, int Version, string[] Tags);
    public record UtteranceNode(Guid Id, string Text, Guid? CharacterId, bool Deleted, string[] Tags);
    public record RelationEdge(Guid From, Guid To, string Type, double? Weight);
    public record GraphResponse(Guid ConversationId, string Title, UtteranceNode[] Nodes, RelationEdge[] Relations);
}   