namespace Npc.Api.Dtos
{
    public record ConversationImportRequest(string? Title, Guid? ConversationId, Guid? RootUtteranceId, bool PreserveIds, ImportedUtterance[] Utterances, ImportedRelation[] Relations);
    public record ImportedUtterance(Guid? Id, string Text, Guid? CharacterId, bool? Deleted, string[]? Tags, int? Version);
    public record ImportedRelation(Guid From, Guid To, string Type, double? Weight);
}