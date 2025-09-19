using Neo4j.Driver;
using Npc.Api.Repositories;

namespace Npc.Api.Domain.Events.Handlers
{
    // Character sync handlers - PostgreSQL to Neo4j
    public class CharacterCreatedEventHandler : IDomainEventHandler<CharacterCreatedEvent>
    {
        private readonly IDriver _neo4jDriver;
        private readonly ILogger<CharacterCreatedEventHandler> _logger;

        public CharacterCreatedEventHandler(IDriver neo4jDriver, ILogger<CharacterCreatedEventHandler> logger)
        {
            _neo4jDriver = neo4jDriver;
            _logger = logger;
        }

        public async Task HandleAsync(CharacterCreatedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                const string cypher = """
                    MERGE (c:Character {id: $id})
                    SET c.name = $name,
                        c.age = $age,
                        c.description = $description,
                        c.avatarUrl = $avatarUrl,
                        c.createdAt = datetime($createdAt),
                        c.updatedAt = datetime($updatedAt),
                        c.syncedAt = datetime()
                    """;

                var character = domainEvent.Character;
                await using var session = _neo4jDriver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
                await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
                {
                    id = character.Id.ToString(),
                    name = character.Name,
                    age = character.Age,
                    description = character.Description ?? "",
                    avatarUrl = character.AvatarUrl ?? "",
                    createdAt = character.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    updatedAt = character.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }));

                _logger.LogDebug("Synced character creation to Neo4j: {CharacterId}", character.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync character creation to Neo4j: {CharacterId}", domainEvent.Character.Id);
                throw;
            }
        }
    }

    public class CharacterUpdatedEventHandler : IDomainEventHandler<CharacterUpdatedEvent>
    {
        private readonly IDriver _neo4jDriver;
        private readonly ILogger<CharacterUpdatedEventHandler> _logger;

        public CharacterUpdatedEventHandler(IDriver neo4jDriver, ILogger<CharacterUpdatedEventHandler> logger)
        {
            _neo4jDriver = neo4jDriver;
            _logger = logger;
        }

        public async Task HandleAsync(CharacterUpdatedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                const string cypher = """
                    MATCH (c:Character {id: $id})
                    SET c.name = $name,
                        c.age = $age,
                        c.description = $description,
                        c.avatarUrl = $avatarUrl,
                        c.updatedAt = datetime($updatedAt),
                        c.syncedAt = datetime()
                    """;

                var character = domainEvent.Character;
                await using var session = _neo4jDriver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
                await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
                {
                    id = character.Id.ToString(),
                    name = character.Name,
                    age = character.Age,
                    description = character.Description ?? "",
                    avatarUrl = character.AvatarUrl ?? "",
                    updatedAt = character.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }));

                _logger.LogDebug("Synced character update to Neo4j: {CharacterId}", character.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync character update to Neo4j: {CharacterId}", domainEvent.Character.Id);
                throw;
            }
        }
    }

    public class CharacterDeletedEventHandler : IDomainEventHandler<CharacterDeletedEvent>
    {
        private readonly IDriver _neo4jDriver;
        private readonly ILogger<CharacterDeletedEventHandler> _logger;

        public CharacterDeletedEventHandler(IDriver neo4jDriver, ILogger<CharacterDeletedEventHandler> logger)
        {
            _neo4jDriver = neo4jDriver;
            _logger = logger;
        }

        public async Task HandleAsync(CharacterDeletedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                const string cypher = """
                    MATCH (c:Character {id: $id})
                    DETACH DELETE c
                    """;

                await using var session = _neo4jDriver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
                await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
                {
                    id = domainEvent.CharacterId.ToString()
                }));

                _logger.LogDebug("Synced character deletion to Neo4j: {CharacterId}", domainEvent.CharacterId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync character deletion to Neo4j: {CharacterId}", domainEvent.CharacterId);
                throw;
            }
        }
    }

    // World sync handlers
    public class WorldCreatedEventHandler : IDomainEventHandler<WorldCreatedEvent>
    {
        private readonly IDriver _neo4jDriver;
        private readonly ILogger<WorldCreatedEventHandler> _logger;

        public WorldCreatedEventHandler(IDriver neo4jDriver, ILogger<WorldCreatedEventHandler> logger)
        {
            _neo4jDriver = neo4jDriver;
            _logger = logger;
        }

        public async Task HandleAsync(WorldCreatedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                const string cypher = """
                    MERGE (w:World {id: $id})
                    SET w.name = $name,
                        w.description = $description,
                        w.createdAt = datetime($createdAt),
                        w.updatedAt = datetime($updatedAt),
                        w.syncedAt = datetime()
                    """;

                var world = domainEvent.World;
                await using var session = _neo4jDriver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
                await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
                {
                    id = world.Id.ToString(),
                    name = world.Name,
                    description = world.Description ?? "",
                    createdAt = world.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    updatedAt = world.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }));

                _logger.LogDebug("Synced world creation to Neo4j: {WorldId}", world.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync world creation to Neo4j: {WorldId}", domainEvent.World.Id);
                throw;
            }
        }
    }

    // Conversation sync handlers - Neo4j to PostgreSQL metadata
    public class ConversationCreatedEventHandler : IDomainEventHandler<ConversationCreatedEvent>
    {
        private readonly ILogger<ConversationCreatedEventHandler> _logger;

        public ConversationCreatedEventHandler(ILogger<ConversationCreatedEventHandler> logger)
        {
            _logger = logger;
        }

        public async Task HandleAsync(ConversationCreatedEvent domainEvent, CancellationToken ct = default)
        {
            // Could sync conversation metadata to PostgreSQL for reporting/analytics
            _logger.LogDebug("Conversation created: {ConversationId} - {Title}",
                domainEvent.ConversationId, domainEvent.Title);

            // Example: Store conversation metadata in PostgreSQL
            // This could include analytics, user tracking, etc.
            await Task.CompletedTask;
        }
    }

    // Cache invalidation handlers
    public class CacheInvalidationHandler :
        IDomainEventHandler<CharacterCreatedEvent>,
        IDomainEventHandler<CharacterUpdatedEvent>,
        IDomainEventHandler<CharacterDeletedEvent>,
        IDomainEventHandler<WorldCreatedEvent>,
        IDomainEventHandler<WorldUpdatedEvent>,
        IDomainEventHandler<WorldDeletedEvent>
    {
        private readonly ILogger<CacheInvalidationHandler> _logger;
        // private readonly IDistributedCache _cache; // Could inject Redis cache

        public CacheInvalidationHandler(ILogger<CacheInvalidationHandler> logger)
        {
            _logger = logger;
        }

        public async Task HandleAsync(CharacterCreatedEvent domainEvent, CancellationToken ct = default)
        {
            // Invalidate character caches
            _logger.LogDebug("Invalidating character caches for: {CharacterId}", domainEvent.Character.Id);
            await Task.CompletedTask;
        }

        public async Task HandleAsync(CharacterUpdatedEvent domainEvent, CancellationToken ct = default)
        {
            _logger.LogDebug("Invalidating character caches for: {CharacterId}", domainEvent.Character.Id);
            await Task.CompletedTask;
        }

        public async Task HandleAsync(CharacterDeletedEvent domainEvent, CancellationToken ct = default)
        {
            _logger.LogDebug("Invalidating character caches for: {CharacterId}", domainEvent.CharacterId);
            await Task.CompletedTask;
        }

        public async Task HandleAsync(WorldCreatedEvent domainEvent, CancellationToken ct = default)
        {
            _logger.LogDebug("Invalidating world caches for: {WorldId}", domainEvent.World.Id);
            await Task.CompletedTask;
        }

        public async Task HandleAsync(WorldUpdatedEvent domainEvent, CancellationToken ct = default)
        {
            _logger.LogDebug("Invalidating world caches for: {WorldId}", domainEvent.World.Id);
            await Task.CompletedTask;
        }

        public async Task HandleAsync(WorldDeletedEvent domainEvent, CancellationToken ct = default)
        {
            _logger.LogDebug("Invalidating world caches for: {WorldId}", domainEvent.WorldId);
            await Task.CompletedTask;
        }
    }
}