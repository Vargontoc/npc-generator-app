namespace Npc.Api.Dtos
{
    public record WorldRequest(string Name, string? Description);
    public record WorldResponse(Guid Id, string Name, string? Description, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}