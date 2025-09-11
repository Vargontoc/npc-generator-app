namespace Npc.Api.Dtos
{

    public record CharacterRequest(string Name, int Age, string? Description, string? AvatarUrl);
    public record CharacterResponse(Guid Id, string Name, int Age, bool IsMinor, string? Description, string? AvatarUrl, DateTimeOffset CreatedAt, DateTimeOffset UpdateAt);
    
}