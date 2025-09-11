using System.ComponentModel.DataAnnotations.Schema;

namespace Npc.Api.Entities
{
    public class Character : BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Name { get; set; }
        public int Age { get; set; }
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }


        [NotMapped]
        public bool IsMinor { get { return Age < 18; } }
    }
}