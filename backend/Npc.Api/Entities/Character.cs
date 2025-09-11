using System.ComponentModel.DataAnnotations.Schema;

namespace Npc.Api.Entities
{
    public class Character
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Name { get; set; }
        public int Age { get; set; }
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdateAt { get; set; }

        [NotMapped]
        public bool IsMinor { get { return Age < 18; } }
    }
}