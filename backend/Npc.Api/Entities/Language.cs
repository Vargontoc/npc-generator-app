using System.ComponentModel.DataAnnotations;

namespace Npc.Api.Entities
{
    public class Language : BaseEntity
    {
        [Required]
        [MaxLength(10)]
        public required string Code { get; set; } // "en", "es", "fr", "de", etc.

        [Required]
        [MaxLength(100)]
        public required string Name { get; set; } // "English", "Español", "Français", etc.

        [Required]
        [MaxLength(100)]
        public required string NativeName { get; set; } // "English", "Español", "Français", etc.

        public bool IsActive { get; set; } = true;

        public bool IsDefault { get; set; } = false;

        public int SortOrder { get; set; } = 0;

        public string? CultureInfo { get; set; } // "en-US", "es-ES", etc.

        public bool IsRightToLeft { get; set; } = false;

        // Navigation properties
        public ICollection<LocalizedContent> LocalizedContents { get; set; } = new List<LocalizedContent>();
    }
}