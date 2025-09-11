using System.IO.Compression;

namespace Npc.Api.Entities
{
    public class World : BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Name { get; set; }
        public string? Description { get; set; }

        public ICollection<Lore> LoreEntries { get; set; } = [];
    }
}