using HotChocolate.Subscriptions;
using HotChocolate.Types;
using Npc.Api.Entities;

namespace Npc.Api.GraphQL.Subscriptions
{
    public class Subscription
    {
        // Character subscriptions
        [Subscribe]
        [Topic("CharacterCreated")]
        public Character OnCharacterCreated([EventMessage] Character character) => character;

        [Subscribe]
        [Topic("CharacterUpdated")]
        public Character OnCharacterUpdated([EventMessage] Character character) => character;

        [Subscribe]
        [Topic("CharacterDeleted")]
        public Guid OnCharacterDeleted([EventMessage] Guid characterId) => characterId;

        [Subscribe]
        [Topic("CharacterGenerated")]
        public Character OnCharacterGenerated([EventMessage] Character character) => character;

        // World subscriptions
        [Subscribe]
        [Topic("WorldCreated")]
        public World OnWorldCreated([EventMessage] World world) => world;

        [Subscribe]
        [Topic("WorldUpdated")]
        public World OnWorldUpdated([EventMessage] World world) => world;

        // Lore subscriptions
        [Subscribe]
        [Topic("LoreCreated")]
        public Lore OnLoreCreated([EventMessage] Lore lore) => lore;

        [Subscribe]
        [Topic("LoreGenerated")]
        public Lore OnLoreGenerated([EventMessage] Lore lore) => lore;

        // Conversation subscriptions
        [Subscribe]
        [Topic("ConversationCreated")]
        public Conversation OnConversationCreated([EventMessage] Conversation conversation) => conversation;

        [Subscribe]
        [Topic("UtteranceAdded")]
        public Utterance OnUtteranceAdded([EventMessage] Utterance utterance) => utterance;

        // Filtered subscriptions
        [Subscribe]
        [Topic("CharacterCreated")]
        public Character OnCharacterCreatedInWorld(
            [EventMessage] Character character,
            Guid worldId) => character.WorldId == worldId ? character : null!;

        [Subscribe]
        [Topic("LoreCreated")]
        public Lore OnLoreCreatedInWorld(
            [EventMessage] Lore lore,
            Guid worldId) => lore.WorldId == worldId ? lore : null!;

        [Subscribe]
        [Topic("ConversationCreated")]
        public Conversation OnConversationCreatedInWorld(
            [EventMessage] Conversation conversation,
            Guid worldId) => conversation.WorldId == worldId ? conversation : null!;
    }
}