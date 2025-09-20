namespace Npc.Api.GraphQL.InputTypes
{
    public record CreateWorldInput(
        string Name,
        string? Description
    );

    public record UpdateWorldInput(
        string? Name,
        string? Description
    );
}