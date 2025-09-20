using Npc.Api.Extensions;

namespace Npc.Api.Entities
{
    public class Conversation : BaseEntity, ILocalizable
    {
        public required string Title { get; set; }
        public Guid? WorldId { get; set; }
        public World? World { get; set; }

        // Navigation properties
        public ICollection<Utterance> Utterances { get; set; } = new List<Utterance>();

        // Localization implementation
        public string GetEntityType() => "Conversation";

        public Dictionary<string, string> GetLocalizableProperties()
        {
            return new Dictionary<string, string>
            {
                { "Title", Title }
            };
        }

        public void ApplyLocalizations(Dictionary<string, string> localizations)
        {
            if (localizations.TryGetValue("Title", out var localizedTitle))
                Title = localizedTitle;
        }
    }
}