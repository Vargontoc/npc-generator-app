namespace Npc.Api.Dtos
{
    public record AutoExpandedRequest(int Count, string? Context, Guid? FromUtteranceId);
    public record GeneratedUtterance(string Text, Guid? CharacterId, string[]? Tags);
}