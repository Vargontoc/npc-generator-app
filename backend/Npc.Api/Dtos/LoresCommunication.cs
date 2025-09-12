namespace Npc.Api.Dtos
{
    public record LoreRequest(string Title, string? Text, Guid? WorldId);
    public record LoreResponse(Guid Id, string Title, string? Text, Guid? WorldId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
    public record LoreSuggestRequest(Guid? WorldId, string Prompt, int Count = 1, bool DryRun = false);

    public record LoreSuggestedItem(string Title, string Text, string? Model);

    public record LoreSuggestResponse(bool Persisted, LoreSuggestedItem[] Items);
    
}