using System.IO.Compression;

namespace Npc.Api.Entities
{
    public class World : BaseEntity
    {
        public required string Name { get; set; }
        public string? Description { get; set; }

        // Navigation properties
        public ICollection<Lore> LoreEntries { get; set; } = [];
        public ICollection<Character> Characters { get; set; } = [];
    }
}