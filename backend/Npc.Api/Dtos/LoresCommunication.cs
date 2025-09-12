namespace Npc.Api.Dtos
{
    public record LoreRequest(string Title, string? Text, Guid? WorldId);
    public record LoreResponse(Guid Id, string Title, string? Text, Guid? WorldId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}