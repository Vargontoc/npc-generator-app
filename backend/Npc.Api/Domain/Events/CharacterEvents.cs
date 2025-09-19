using Npc.Api.Entities;

namespace Npc.Api.Domain.Events
{
    // Character Domain Events
    public record CharacterCreatedEvent(Character Character) : DomainEvent;
    public record CharacterUpdatedEvent(Character Character, object OldValues) : DomainEvent;
    public record CharacterDeletedEvent(Guid CharacterId, object DeletedValues) : DomainEvent;

    // World Domain Events
    public record WorldCreatedEvent(World World) : DomainEvent;
    public record WorldUpdatedEvent(World World, object OldValues) : DomainEvent;
    public record WorldDeletedEvent(Guid WorldId, object DeletedValues) : DomainEvent;

    // Lore Domain Events
    public record LoreCreatedEvent(Lore Lore) : DomainEvent;
    public record LoreUpdatedEvent(Lore Lore, object OldValues) : DomainEvent;
    public record LoreDeletedEvent(Guid LoreId, object DeletedValues) : DomainEvent;

    // Conversation Domain Events (for Neo4j sync)
    public record ConversationCreatedEvent(Guid ConversationId, string Title) : DomainEvent;
    public record UtteranceCreatedEvent(Guid UtteranceId, Guid ConversationId, string Text, Guid? CharacterId) : DomainEvent;
    public record UtteranceUpdatedEvent(Guid UtteranceId, string Text, string[]? Tags, int Version) : DomainEvent;
    public record UtteranceDeletedEvent(Guid UtteranceId) : DomainEvent;
}