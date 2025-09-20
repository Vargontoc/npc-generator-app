using Npc.Api.Entities;
using Npc.Api.Repositories;
using Npc.Api.Infrastructure.Cache;

namespace Npc.Api.Application.Queries
{
    // Cached Lore Query Handlers
    public class CachedGetLoreByIdQueryHandler : IQueryHandler<GetLoreByIdQuery, Lore?>
    {
        private readonly ILoreRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedGetLoreByIdQueryHandler> _logger;

        public CachedGetLoreByIdQueryHandler(
            ILoreRepository repository,
            ICacheService cache,
            ILogger<CachedGetLoreByIdQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<Lore?> HandleAsync(GetLoreByIdQuery query, CancellationToken ct = default)
        {
            var cacheKey = CacheKeys.Lore(query.Id);

            // Try to get from cache first
            var cached = await _cache.GetAsync<Lore>(cacheKey, ct);
            if (cached is not null)
            {
                _logger.LogDebug("Lore {LoreId} found in cache", query.Id);
                return cached;
            }

            // Not in cache, get from repository
            var lore = await _repository.GetByIdAsync(query.Id, ct);
            if (lore is not null)
            {
                // Cache for 20 minutes (lore content is relatively stable)
                await _cache.SetAsync(cacheKey, lore, TimeSpan.FromMinutes(20), ct);
                _logger.LogDebug("Lore {LoreId} cached for 20 minutes", query.Id);
            }

            return lore;
        }
    }

    public class CachedGetLoreByWorldIdQueryHandler : IQueryHandler<GetLoreByWorldIdQuery, IEnumerable<Lore>>
    {
        private readonly ILoreRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedGetLoreByWorldIdQueryHandler> _logger;

        public CachedGetLoreByWorldIdQueryHandler(
            ILoreRepository repository,
            ICacheService cache,
            ILogger<CachedGetLoreByWorldIdQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IEnumerable<Lore>> HandleAsync(GetLoreByWorldIdQuery query, CancellationToken ct = default)
        {
            var cacheKey = CacheKeys.LoreByWorld(query.WorldId);

            // Try to get from cache first
            var cached = await _cache.GetAsync<IEnumerable<Lore>>(cacheKey, ct);
            if (cached is not null)
            {
                _logger.LogDebug("Lore by world {WorldId} found in cache", query.WorldId);
                return cached;
            }

            // Not in cache, get from repository
            var lore = await _repository.GetByWorldIdAsync(query.WorldId, ct);

            // Cache for 15 minutes
            await _cache.SetAsync(cacheKey, lore, TimeSpan.FromMinutes(15), ct);
            _logger.LogDebug("Lore by world {WorldId} cached for 15 minutes", query.WorldId);

            return lore;
        }
    }

    public class CachedGetGeneratedLoreQueryHandler : IQueryHandler<GetGeneratedLoreQuery, IEnumerable<Lore>>
    {
        private readonly ILoreRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedGetGeneratedLoreQueryHandler> _logger;

        public CachedGetGeneratedLoreQueryHandler(
            ILoreRepository repository,
            ICacheService cache,
            ILogger<CachedGetGeneratedLoreQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IEnumerable<Lore>> HandleAsync(GetGeneratedLoreQuery query, CancellationToken ct = default)
        {
            var cacheKey = CacheKeys.LoreGenerated();

            // Try to get from cache first
            var cached = await _cache.GetAsync<IEnumerable<Lore>>(cacheKey, ct);
            if (cached is not null)
            {
                _logger.LogDebug("Generated lore found in cache");
                return cached;
            }

            // Not in cache, get from repository
            var lore = await _repository.GetGeneratedLoreAsync(ct);

            // Cache for 10 minutes (generated content might change more frequently)
            await _cache.SetAsync(cacheKey, lore, TimeSpan.FromMinutes(10), ct);
            _logger.LogDebug("Generated lore cached for 10 minutes");

            return lore;
        }
    }

    public class CachedSearchLoreByTextQueryHandler : IQueryHandler<SearchLoreByTextQuery, IEnumerable<Lore>>
    {
        private readonly ILoreRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedSearchLoreByTextQueryHandler> _logger;

        public CachedSearchLoreByTextQueryHandler(
            ILoreRepository repository,
            ICacheService cache,
            ILogger<CachedSearchLoreByTextQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IEnumerable<Lore>> HandleAsync(SearchLoreByTextQuery query, CancellationToken ct = default)
        {
            var cacheKey = CacheKeys.LoreSearch(query.SearchText, 1, 100); // Default pagination for search

            // Try to get from cache first
            var cached = await _cache.GetAsync<IEnumerable<Lore>>(cacheKey, ct);
            if (cached is not null)
            {
                _logger.LogDebug("Lore search for '{SearchText}' found in cache", query.SearchText);
                return cached;
            }

            // Not in cache, get from repository
            var lore = await _repository.SearchByTextAsync(query.SearchText, ct);

            // Cache for 10 minutes
            await _cache.SetAsync(cacheKey, lore, TimeSpan.FromMinutes(10), ct);
            _logger.LogDebug("Lore search for '{SearchText}' cached for 10 minutes", query.SearchText);

            return lore;
        }
    }
}