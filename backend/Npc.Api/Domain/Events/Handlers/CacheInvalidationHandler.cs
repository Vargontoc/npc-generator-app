using Npc.Api.Infrastructure.Cache;

namespace Npc.Api.Domain.Events.Handlers
{
    public class CacheInvalidationHandler :
        IDomainEventHandler<CharacterCreatedEvent>,
        IDomainEventHandler<CharacterUpdatedEvent>,
        IDomainEventHandler<CharacterDeletedEvent>,
        IDomainEventHandler<WorldCreatedEvent>,
        IDomainEventHandler<WorldUpdatedEvent>,
        IDomainEventHandler<WorldDeletedEvent>,
        IDomainEventHandler<LoreCreatedEvent>,
        IDomainEventHandler<LoreUpdatedEvent>,
        IDomainEventHandler<LoreDeletedEvent>
    {
        private readonly ICacheService _cache;
        private readonly ILogger<CacheInvalidationHandler> _logger;

        public CacheInvalidationHandler(ICacheService cache, ILogger<CacheInvalidationHandler> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        // Character Events
        public async Task HandleAsync(CharacterCreatedEvent domainEvent, CancellationToken ct = default)
        {
            await InvalidateCachePatterns("character", domainEvent.Character.Id, null, ct);
            _logger.LogDebug("Cache invalidated for character creation: {CharacterId}", domainEvent.Character.Id);
        }

        public async Task HandleAsync(CharacterUpdatedEvent domainEvent, CancellationToken ct = default)
        {
            await InvalidateCachePatterns("character", domainEvent.Character.Id, null, ct);
            _logger.LogDebug("Cache invalidated for character update: {CharacterId}", domainEvent.Character.Id);
        }

        public async Task HandleAsync(CharacterDeletedEvent domainEvent, CancellationToken ct = default)
        {
            await InvalidateCachePatterns("character", domainEvent.CharacterId, null, ct);
            _logger.LogDebug("Cache invalidated for character deletion: {CharacterId}", domainEvent.CharacterId);
        }

        // World Events
        public async Task HandleAsync(WorldCreatedEvent domainEvent, CancellationToken ct = default)
        {
            await InvalidateCachePatterns("world", domainEvent.World.Id, null, ct);
            _logger.LogDebug("Cache invalidated for world creation: {WorldId}", domainEvent.World.Id);
        }

        public async Task HandleAsync(WorldUpdatedEvent domainEvent, CancellationToken ct = default)
        {
            await InvalidateCachePatterns("world", domainEvent.World.Id, null, ct);
            _logger.LogDebug("Cache invalidated for world update: {WorldId}", domainEvent.World.Id);
        }

        public async Task HandleAsync(WorldDeletedEvent domainEvent, CancellationToken ct = default)
        {
            await InvalidateCachePatterns("world", domainEvent.WorldId, null, ct);
            _logger.LogDebug("Cache invalidated for world deletion: {WorldId}", domainEvent.WorldId);
        }

        // Lore Events
        public async Task HandleAsync(LoreCreatedEvent domainEvent, CancellationToken ct = default)
        {
            await InvalidateCachePatterns("lore", domainEvent.Lore.Id, domainEvent.Lore.WorldId, ct);
            _logger.LogDebug("Cache invalidated for lore creation: {LoreId}, WorldId: {WorldId}",
                domainEvent.Lore.Id, domainEvent.Lore.WorldId);
        }

        public async Task HandleAsync(LoreUpdatedEvent domainEvent, CancellationToken ct = default)
        {
            await InvalidateCachePatterns("lore", domainEvent.Lore.Id, domainEvent.Lore.WorldId, ct);
            _logger.LogDebug("Cache invalidated for lore update: {LoreId}, WorldId: {WorldId}",
                domainEvent.Lore.Id, domainEvent.Lore.WorldId);
        }

        public async Task HandleAsync(LoreDeletedEvent domainEvent, CancellationToken ct = default)
        {
            // Extract WorldId from the deleted values if available
            var worldId = domainEvent.DeletedValues != null && domainEvent.DeletedValues.GetType().GetProperty("WorldId") != null
                ? (Guid?)domainEvent.DeletedValues.GetType().GetProperty("WorldId")?.GetValue(domainEvent.DeletedValues)
                : null;

            await InvalidateCachePatterns("lore", domainEvent.LoreId, worldId, ct);
            _logger.LogDebug("Cache invalidated for lore deletion: {LoreId}, WorldId: {WorldId}",
                domainEvent.LoreId, worldId);
        }

        private async Task InvalidateCachePatterns(string entityType, Guid entityId, Guid? worldId, CancellationToken ct)
        {
            try
            {
                var patterns = CacheKeys.GetInvalidationPatterns(entityType, entityId, worldId);

                var invalidationTasks = patterns.Select(pattern =>
                    _cache.RemoveByPatternAsync(pattern, ct)).ToArray();

                await Task.WhenAll(invalidationTasks);

                _logger.LogDebug("Cache patterns invalidated for {EntityType} {EntityId}: [{Patterns}]",
                    entityType, entityId, string.Join(", ", patterns));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate cache patterns for {EntityType} {EntityId}",
                    entityType, entityId);
                // Don't throw - cache invalidation failure shouldn't break the application
            }
        }
    }
}