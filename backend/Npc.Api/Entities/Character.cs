using System.ComponentModel.DataAnnotations.Schema;
using Npc.Api.Extensions;

namespace Npc.Api.Entities
{
    public class Character : BaseEntity, ILocalizable
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

        // Localization implementation
        public string GetEntityType() => "Character";

        public Dictionary<string, string> GetLocalizableProperties()
        {
            return new Dictionary<string, string>
            {
                { "Name", Name },
                { "Description", Description ?? string.Empty }
            };
        }

        public void ApplyLocalizations(Dictionary<string, string> localizations)
        {
            if (localizations.TryGetValue("Name", out var localizedName))
                Name = localizedName;

            if (localizations.TryGetValue("Description", out var localizedDescription))
                Description = localizedDescription;
        }
    }
}