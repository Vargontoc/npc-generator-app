using Npc.Api.Entities;
using Npc.Api.Repositories;

namespace Npc.Api.Application.Queries
{
    // Query DTOs
    public record GetCharacterByIdQuery(Guid Id) : IQuery<Character?>;
    public record GetCharactersPagedQuery(int Page, int PageSize) : IQuery<(IEnumerable<Character> Items, int TotalCount)>;
    public record GetCharactersByAgeRangeQuery(int MinAge, int MaxAge) : IQuery<IEnumerable<Character>>;
    public record SearchCharactersByNameQuery(string NamePattern) : IQuery<IEnumerable<Character>>;

    // Query Handlers
    public class GetCharacterByIdQueryHandler : IQueryHandler<GetCharacterByIdQuery, Character?>
    {
        private readonly ICharacterRepository _repository;

        public GetCharacterByIdQueryHandler(ICharacterRepository repository)
        {
            _repository = repository;
        }

        public async Task<Character?> HandleAsync(GetCharacterByIdQuery query, CancellationToken ct = default)
        {
            return await _repository.GetByIdAsync(query.Id, ct);
        }
    }

    public class GetCharactersPagedQueryHandler : IQueryHandler<GetCharactersPagedQuery, (IEnumerable<Character> Items, int TotalCount)>
    {
        private readonly ICharacterRepository _repository;

        public GetCharactersPagedQueryHandler(ICharacterRepository repository)
        {
            _repository = repository;
        }

        public async Task<(IEnumerable<Character> Items, int TotalCount)> HandleAsync(GetCharactersPagedQuery query, CancellationToken ct = default)
        {
            return await _repository.GetPagedAsync(query.Page, query.PageSize, ct);
        }
    }

    public class GetCharactersByAgeRangeQueryHandler : IQueryHandler<GetCharactersByAgeRangeQuery, IEnumerable<Character>>
    {
        private readonly ICharacterRepository _repository;

        public GetCharactersByAgeRangeQueryHandler(ICharacterRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<Character>> HandleAsync(GetCharactersByAgeRangeQuery query, CancellationToken ct = default)
        {
            return await _repository.GetByAgeRangeAsync(query.MinAge, query.MaxAge, ct);
        }
    }

    public class SearchCharactersByNameQueryHandler : IQueryHandler<SearchCharactersByNameQuery, IEnumerable<Character>>
    {
        private readonly ICharacterRepository _repository;

        public SearchCharactersByNameQueryHandler(ICharacterRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<Character>> HandleAsync(SearchCharactersByNameQuery query, CancellationToken ct = default)
        {
            return await _repository.SearchByNameAsync(query.NamePattern, ct);
        }
    }
}