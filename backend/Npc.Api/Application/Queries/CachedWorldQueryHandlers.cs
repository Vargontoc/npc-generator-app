using Npc.Api.Entities;
using Npc.Api.Repositories;
using Npc.Api.Infrastructure.Cache;

namespace Npc.Api.Application.Queries
{
    // Cached World Query Handlers
    public class CachedGetWorldByIdQueryHandler : IQueryHandler<GetWorldByIdQuery, World?>
    {
        private readonly IWorldRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedGetWorldByIdQueryHandler> _logger;

        public CachedGetWorldByIdQueryHandler(
            IWorldRepository repository,
            ICacheService cache,
            ILogger<CachedGetWorldByIdQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<World?> HandleAsync(GetWorldByIdQuery query, CancellationToken ct = default)
        {
            var cacheKey = CacheKeys.World(query.Id);

            // Try to get from cache first
            var cached = await _cache.GetAsync<World>(cacheKey, ct);
            if (cached is not null)
            {
                _logger.LogDebug("World {WorldId} found in cache", query.Id);
                return cached;
            }

            // Not in cache, get from repository
            var world = await _repository.GetByIdAsync(query.Id, ct);
            if (world is not null)
            {
                // Cache for 20 minutes (worlds change less frequently)
                await _cache.SetAsync(cacheKey, world, TimeSpan.FromMinutes(20), ct);
                _logger.LogDebug("World {WorldId} cached for 20 minutes", query.Id);
            }

            return world;
        }
    }

    public class CachedGetWorldsPagedQueryHandler : IQueryHandler<GetWorldsPagedQuery, (IEnumerable<World> Items, int TotalCount)>
    {
        private readonly IWorldRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedGetWorldsPagedQueryHandler> _logger;

        public CachedGetWorldsPagedQueryHandler(
            IWorldRepository repository,
            ICacheService cache,
            ILogger<CachedGetWorldsPagedQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<(IEnumerable<World> Items, int TotalCount)> HandleAsync(GetWorldsPagedQuery query, CancellationToken ct = default)
        {
            var cacheKey = CacheKeys.WorldsPaged(query.Page, query.PageSize);

            // Try to get from cache first
            var cached = await _cache.GetAsync<(IEnumerable<World> Items, int TotalCount)>(cacheKey, ct);
            if (cached.Items is not null)
            {
                _logger.LogDebug("Paged worlds (page {Page}, size {PageSize}) found in cache", query.Page, query.PageSize);
                return cached;
            }

            // Not in cache, get from repository
            var result = await _repository.GetPagedAsync(query.Page, query.PageSize, ct);

            // Cache for 10 minutes
            await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10), ct);
            _logger.LogDebug("Paged worlds (page {Page}, size {PageSize}) cached for 10 minutes", query.Page, query.PageSize);

            return result;
        }
    }

    public class CachedGetWorldsWithLoreQueryHandler : IQueryHandler<GetWorldsWithLoreQuery, IEnumerable<World>>
    {
        private readonly IWorldRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedGetWorldsWithLoreQueryHandler> _logger;

        public CachedGetWorldsWithLoreQueryHandler(
            IWorldRepository repository,
            ICacheService cache,
            ILogger<CachedGetWorldsWithLoreQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IEnumerable<World>> HandleAsync(GetWorldsWithLoreQuery query, CancellationToken ct = default)
        {
            var cacheKey = CacheKeys.WorldsWithLore();

            // Try to get from cache first
            var cached = await _cache.GetAsync<IEnumerable<World>>(cacheKey, ct);
            if (cached is not null)
            {
                _logger.LogDebug("Worlds with lore found in cache");
                return cached;
            }

            // Not in cache, get from repository
            var worlds = await _repository.GetWorldsWithLoreAsync(ct);

            // Cache for 15 minutes
            await _cache.SetAsync(cacheKey, worlds, TimeSpan.FromMinutes(15), ct);
            _logger.LogDebug("Worlds with lore cached for 15 minutes");

            return worlds;
        }
    }

    public class CachedGetWorldWithLoreByIdQueryHandler : IQueryHandler<GetWorldWithLoreByIdQuery, World?>
    {
        private readonly IWorldRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedGetWorldWithLoreByIdQueryHandler> _logger;

        public CachedGetWorldWithLoreByIdQueryHandler(
            IWorldRepository repository,
            ICacheService cache,
            ILogger<CachedGetWorldWithLoreByIdQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<World?> HandleAsync(GetWorldWithLoreByIdQuery query, CancellationToken ct = default)
        {
            var cacheKey = CacheKeys.WorldWithLore(query.Id);

            // Try to get from cache first
            var cached = await _cache.GetAsync<World>(cacheKey, ct);
            if (cached is not null)
            {
                _logger.LogDebug("World with lore {WorldId} found in cache", query.Id);
                return cached;
            }

            // Not in cache, get from repository
            var world = await _repository.GetWorldWithLoreByIdAsync(query.Id, ct);
            if (world is not null)
            {
                // Cache for 15 minutes
                await _cache.SetAsync(cacheKey, world, TimeSpan.FromMinutes(15), ct);
                _logger.LogDebug("World with lore {WorldId} cached for 15 minutes", query.Id);
            }

            return world;
        }
    }
}