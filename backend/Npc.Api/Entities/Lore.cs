namespace Npc.Api.Entities
{
    public class Lore : BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Title { get; set; }
        public string? Text { get; set; }
        public Guid? WorldId { get; set; }
        public World? World { get; set; }
    }
}