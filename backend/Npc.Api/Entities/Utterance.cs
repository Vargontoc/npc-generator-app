using Npc.Api.Extensions;

namespace Npc.Api.Entities
{
    public class Utterance : BaseEntity, ILocalizable
    {
        public required string Text { get; set; }
        public Guid ConversationId { get; set; }
        public Guid? CharacterId { get; set; }
        public int Version { get; set; } = 1;
        public bool Deleted { get; set; } = false;
        public string[] Tags { get; set; } = Array.Empty<string>();

        // Navigation properties
        public Conversation? Conversation { get; set; }
        public Character? Character { get; set; }

        // Localization implementation
        public string GetEntityType() => "Utterance";

        public Dictionary<string, string> GetLocalizableProperties()
        {
            return new Dictionary<string, string>
            {
                { "Text", Text }
            };
        }

        public void ApplyLocalizations(Dictionary<string, string> localizations)
        {
            if (localizations.TryGetValue("Text", out var localizedText))
                Text = localizedText;
        }
    }
}