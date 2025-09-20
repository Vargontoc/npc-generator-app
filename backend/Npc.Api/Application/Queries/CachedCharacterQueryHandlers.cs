using AutoMapper;
using Npc.Api.Entities;
using Npc.Api.Repositories;
using Npc.Api.Infrastructure.Cache;

namespace Npc.Api.Application.Queries
{
    // Cached Character Query Handlers
    public class CachedGetCharacterByIdQueryHandler : IQueryHandler<GetCharacterByIdQuery, Character?>
    {
        private readonly ICharacterRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedGetCharacterByIdQueryHandler> _logger;

        public CachedGetCharacterByIdQueryHandler(
            ICharacterRepository repository,
            ICacheService cache,
            ILogger<CachedGetCharacterByIdQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<Character?> HandleAsync(GetCharacterByIdQuery query, CancellationToken ct = default)
        {
            var cacheKey = CacheKeys.Character(query.Id);

            // Try to get from cache first
            var cached = await _cache.GetAsync<Character>(cacheKey, ct);
            if (cached is not null)
            {
                _logger.LogDebug("Character {CharacterId} found in cache", query.Id);
                return cached;
            }

            // Not in cache, get from repository
            var character = await _repository.GetByIdAsync(query.Id, ct);
            if (character is not null)
            {
                // Cache for 15 minutes
                await _cache.SetAsync(cacheKey, character, TimeSpan.FromMinutes(15), ct);
                _logger.LogDebug("Character {CharacterId} cached for 15 minutes", query.Id);
            }

            return character;
        }
    }

    public class CachedGetCharactersPagedQueryHandler : IQueryHandler<GetCharactersPagedQuery, (IEnumerable<Character> Items, int TotalCount)>
    {
        private readonly ICharacterRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedGetCharactersPagedQueryHandler> _logger;

        public CachedGetCharactersPagedQueryHandler(
            ICharacterRepository repository,
            ICacheService cache,
            ILogger<CachedGetCharactersPagedQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<(IEnumerable<Character> Items, int TotalCount)> HandleAsync(GetCharactersPagedQuery query, CancellationToken ct = default)
        {
            var cacheKey = $"characters:paged:{query.Page}:{query.PageSize}";

            // Try to get from cache first
            var cached = await _cache.GetAsync<(IEnumerable<Character> Items, int TotalCount)>(cacheKey, ct);
            if (cached.Items is not null)
            {
                _logger.LogDebug("Paged characters (page {Page}, size {PageSize}) found in cache", query.Page, query.PageSize);
                return cached;
            }

            // Not in cache, get from repository
            var result = await _repository.GetPagedAsync(query.Page, query.PageSize, ct);

            // Cache for 5 minutes (shorter for lists that change frequently)
            await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), ct);
            _logger.LogDebug("Paged characters (page {Page}, size {PageSize}) cached for 5 minutes", query.Page, query.PageSize);

            return result;
        }
    }

    public class CachedGetCharactersByAgeRangeQueryHandler : IQueryHandler<GetCharactersByAgeRangeQuery, IEnumerable<Character>>
    {
        private readonly ICharacterRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedGetCharactersByAgeRangeQueryHandler> _logger;

        public CachedGetCharactersByAgeRangeQueryHandler(
            ICharacterRepository repository,
            ICacheService cache,
            ILogger<CachedGetCharactersByAgeRangeQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IEnumerable<Character>> HandleAsync(GetCharactersByAgeRangeQuery query, CancellationToken ct = default)
        {
            var cacheKey = CacheKeys.CharactersByAgeRange(query.MinAge, query.MaxAge, 1, 100); // Default pagination for age range

            // Try to get from cache first
            var cached = await _cache.GetAsync<IEnumerable<Character>>(cacheKey, ct);
            if (cached is not null)
            {
                _logger.LogDebug("Characters by age range {MinAge}-{MaxAge} found in cache", query.MinAge, query.MaxAge);
                return cached;
            }

            // Not in cache, get from repository
            var characters = await _repository.GetByAgeRangeAsync(query.MinAge, query.MaxAge, ct);

            // Cache for 10 minutes
            await _cache.SetAsync(cacheKey, characters, TimeSpan.FromMinutes(10), ct);
            _logger.LogDebug("Characters by age range {MinAge}-{MaxAge} cached for 10 minutes", query.MinAge, query.MaxAge);

            return characters;
        }
    }

    public class CachedSearchCharactersByNameQueryHandler : IQueryHandler<SearchCharactersByNameQuery, IEnumerable<Character>>
    {
        private readonly ICharacterRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<CachedSearchCharactersByNameQueryHandler> _logger;

        public CachedSearchCharactersByNameQueryHandler(
            ICharacterRepository repository,
            ICacheService cache,
            ILogger<CachedSearchCharactersByNameQueryHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IEnumerable<Character>> HandleAsync(SearchCharactersByNameQuery query, CancellationToken ct = default)
        {
            var cacheKey = CacheKeys.CharactersSearch(query.Name, 1, 100); // Default pagination for search

            // Try to get from cache first
            var cached = await _cache.GetAsync<IEnumerable<Character>>(cacheKey, ct);
            if (cached is not null)
            {
                _logger.LogDebug("Character search for '{Name}' found in cache", query.Name);
                return cached;
            }

            // Not in cache, get from repository
            var characters = await _repository.SearchByNameAsync(query.Name, ct);

            // Cache for 10 minutes
            await _cache.SetAsync(cacheKey, characters, TimeSpan.FromMinutes(10), ct);
            _logger.LogDebug("Character search for '{Name}' cached for 10 minutes", query.Name);

            return characters;
        }
    }
}