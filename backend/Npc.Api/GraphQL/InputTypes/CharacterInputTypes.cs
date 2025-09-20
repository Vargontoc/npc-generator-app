namespace Npc.Api.GraphQL.InputTypes
{
    public record CreateCharacterInput(
        string Name,
        int Age,
        string? Description,
        Guid? WorldId
    );

    public record UpdateCharacterInput(
        string? Name,
        int? Age,
        string? Description,
        Guid? WorldId
    );

    public record GenerateCharacterInput(
        string? Concept,
        string? Style,
        Guid? WorldId
    );
}