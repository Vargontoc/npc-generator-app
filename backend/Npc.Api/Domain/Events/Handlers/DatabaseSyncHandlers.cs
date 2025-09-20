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

    public class WorldUpdatedEventHandler : IDomainEventHandler<WorldUpdatedEvent>
    {
        private readonly IDriver _neo4jDriver;
        private readonly ILogger<WorldUpdatedEventHandler> _logger;

        public WorldUpdatedEventHandler(IDriver neo4jDriver, ILogger<WorldUpdatedEventHandler> logger)
        {
            _neo4jDriver = neo4jDriver;
            _logger = logger;
        }

        public async Task HandleAsync(WorldUpdatedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                const string cypher = """
                    MATCH (w:World {id: $id})
                    SET w.name = $name,
                        w.description = $description,
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
                    updatedAt = world.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }));

                _logger.LogDebug("Synced world update to Neo4j: {WorldId}", world.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync world update to Neo4j: {WorldId}", domainEvent.World.Id);
                throw;
            }
        }
    }

    public class WorldDeletedEventHandler : IDomainEventHandler<WorldDeletedEvent>
    {
        private readonly IDriver _neo4jDriver;
        private readonly ILogger<WorldDeletedEventHandler> _logger;

        public WorldDeletedEventHandler(IDriver neo4jDriver, ILogger<WorldDeletedEventHandler> logger)
        {
            _neo4jDriver = neo4jDriver;
            _logger = logger;
        }

        public async Task HandleAsync(WorldDeletedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                const string cypher = """
                    MATCH (w:World {id: $id})
                    OPTIONAL MATCH (w)-[r]-()
                    DELETE r, w
                    """;

                await using var session = _neo4jDriver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
                await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
                {
                    id = domainEvent.WorldId.ToString()
                }));

                _logger.LogDebug("Synced world deletion to Neo4j: {WorldId}", domainEvent.WorldId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync world deletion to Neo4j: {WorldId}", domainEvent.WorldId);
                throw;
            }
        }
    }

    // Lore sync handlers - PostgreSQL to Neo4j
    public class LoreCreatedEventHandler : IDomainEventHandler<LoreCreatedEvent>
    {
        private readonly IDriver _neo4jDriver;
        private readonly ILogger<LoreCreatedEventHandler> _logger;

        public LoreCreatedEventHandler(IDriver neo4jDriver, ILogger<LoreCreatedEventHandler> logger)
        {
            _neo4jDriver = neo4jDriver;
            _logger = logger;
        }

        public async Task HandleAsync(LoreCreatedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                const string cypher = """
                    MERGE (l:Lore {id: $id})
                    SET l.title = $title,
                        l.text = $text,
                        l.isGenerated = $isGenerated,
                        l.generationSource = $generationSource,
                        l.createdAt = datetime($createdAt),
                        l.updatedAt = datetime($updatedAt),
                        l.syncedAt = datetime()
                    WITH l
                    OPTIONAL MATCH (w:World {id: $worldId})
                    FOREACH (world IN CASE WHEN w IS NOT NULL THEN [w] ELSE [] END |
                        MERGE (l)-[:BELONGS_TO]->(world)
                    )
                    """;

                var lore = domainEvent.Lore;
                await using var session = _neo4jDriver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
                await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
                {
                    id = lore.Id.ToString(),
                    title = lore.Title,
                    text = lore.Text ?? "",
                    isGenerated = lore.IsGenerated,
                    generationSource = lore.GenerationSource ?? "",
                    worldId = lore.WorldId?.ToString(),
                    createdAt = lore.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    updatedAt = lore.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }));

                _logger.LogDebug("Synced lore creation to Neo4j: {LoreId}", lore.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync lore creation to Neo4j: {LoreId}", domainEvent.Lore.Id);
                throw;
            }
        }
    }

    public class LoreUpdatedEventHandler : IDomainEventHandler<LoreUpdatedEvent>
    {
        private readonly IDriver _neo4jDriver;
        private readonly ILogger<LoreUpdatedEventHandler> _logger;

        public LoreUpdatedEventHandler(IDriver neo4jDriver, ILogger<LoreUpdatedEventHandler> logger)
        {
            _neo4jDriver = neo4jDriver;
            _logger = logger;
        }

        public async Task HandleAsync(LoreUpdatedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                const string cypher = """
                    MATCH (l:Lore {id: $id})
                    SET l.title = $title,
                        l.text = $text,
                        l.updatedAt = datetime($updatedAt),
                        l.syncedAt = datetime()
                    WITH l
                    // Remove old world relationship
                    OPTIONAL MATCH (l)-[oldRel:BELONGS_TO]->()
                    DELETE oldRel
                    WITH l
                    // Add new world relationship if worldId provided
                    OPTIONAL MATCH (w:World {id: $worldId})
                    FOREACH (world IN CASE WHEN w IS NOT NULL THEN [w] ELSE [] END |
                        MERGE (l)-[:BELONGS_TO]->(world)
                    )
                    """;

                var lore = domainEvent.Lore;
                await using var session = _neo4jDriver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
                await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
                {
                    id = lore.Id.ToString(),
                    title = lore.Title,
                    text = lore.Text ?? "",
                    worldId = lore.WorldId?.ToString(),
                    updatedAt = lore.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }));

                _logger.LogDebug("Synced lore update to Neo4j: {LoreId}", lore.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync lore update to Neo4j: {LoreId}", domainEvent.Lore.Id);
                throw;
            }
        }
    }

    public class LoreDeletedEventHandler : IDomainEventHandler<LoreDeletedEvent>
    {
        private readonly IDriver _neo4jDriver;
        private readonly ILogger<LoreDeletedEventHandler> _logger;

        public LoreDeletedEventHandler(IDriver neo4jDriver, ILogger<LoreDeletedEventHandler> logger)
        {
            _neo4jDriver = neo4jDriver;
            _logger = logger;
        }

        public async Task HandleAsync(LoreDeletedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                const string cypher = """
                    MATCH (l:Lore {id: $id})
                    DETACH DELETE l
                    """;

                await using var session = _neo4jDriver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
                await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
                {
                    id = domainEvent.LoreId.ToString()
                }));

                _logger.LogDebug("Synced lore deletion to Neo4j: {LoreId}", domainEvent.LoreId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync lore deletion to Neo4j: {LoreId}", domainEvent.LoreId);
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

}