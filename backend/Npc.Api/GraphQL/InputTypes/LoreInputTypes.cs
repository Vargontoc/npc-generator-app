namespace Npc.Api.GraphQL.InputTypes
{
    public record CreateLoreInput(
        string Title,
        string? Text,
        Guid? WorldId,
        bool IsGenerated = false,
        string? GenerationSource = null,
        string? GenerationMeta = null
    );

    public record UpdateLoreInput(
        string? Title,
        string? Text,
        Guid? WorldId
    );

    public record GenerateLoreInput(
        string Topic,
        string? Style,
        Guid? WorldId
    );
}