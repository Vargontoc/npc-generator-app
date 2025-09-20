using System.IO.Compression;
using Npc.Api.Extensions;

namespace Npc.Api.Entities
{
    public class World : BaseEntity, ILocalizable
    {
        public required string Name { get; set; }
        public string? Description { get; set; }

        // Navigation properties
        public ICollection<Lore> LoreEntries { get; set; } = [];
        public ICollection<Character> Characters { get; set; } = [];

        // Localization implementation
        public string GetEntityType() => "World";

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