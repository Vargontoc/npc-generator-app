using System.ComponentModel.DataAnnotations;

namespace Npc.Api.Entities
{
    public class LocalizedContent : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public required string EntityType { get; set; } // "Character", "Lore", "Dialogue", etc.

        [Required]
        public required Guid EntityId { get; set; } // ID of the target entity

        [Required]
        [MaxLength(100)]
        public required string PropertyName { get; set; } // "Name", "Description", "Title", "Text", etc.

        [Required]
        [MaxLength(10)]
        public required string LanguageCode { get; set; } // "en", "es", "fr", "de", etc.

        [Required]
        public required string Content { get; set; } // The localized text content

        public bool IsDefault { get; set; } // True if this is the default language version

        public string? Notes { get; set; } // Optional notes for translators

        public DateTimeOffset? TranslatedAt { get; set; }

        public string? TranslatedBy { get; set; } // User ID or system that created the translation
    }
}