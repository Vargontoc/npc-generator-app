using System.ComponentModel.DataAnnotations.Schema;

namespace Npc.Api.Entities
{
    public class Character : BaseEntity
    {
        public required string Name { get; set; }
        public int Age { get; set; }
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }
        public string? ImageUrl { get; set; }
        public Guid? WorldId { get; set; }

        // Navigation properties
        public World? World { get; set; }

        [NotMapped]
        public bool IsMinor { get { return Age < 18; } }
    }
}