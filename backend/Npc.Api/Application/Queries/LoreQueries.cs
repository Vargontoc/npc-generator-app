using Npc.Api.Entities;
using Npc.Api.Repositories;

namespace Npc.Api.Application.Queries
{
    // Query DTOs
    public record GetLoreByIdQuery(Guid Id) : IQuery<Lore?>;
    public record GetLoreByWorldIdQuery(Guid? WorldId) : IQuery<IEnumerable<Lore>>;
    public record GetGeneratedLoreQuery() : IQuery<IEnumerable<Lore>>;
    public record SearchLoreByTextQuery(string SearchTerm) : IQuery<IEnumerable<Lore>>;

    // Query Handlers
    public class GetLoreByIdQueryHandler : IQueryHandler<GetLoreByIdQuery, Lore?>
    {
        private readonly ILoreRepository _repository;

        public GetLoreByIdQueryHandler(ILoreRepository repository)
        {
            _repository = repository;
        }

        public async Task<Lore?> HandleAsync(GetLoreByIdQuery query, CancellationToken ct = default)
        {
            return await _repository.GetByIdAsync(query.Id, ct);
        }
    }

    public class GetLoreByWorldIdQueryHandler : IQueryHandler<GetLoreByWorldIdQuery, IEnumerable<Lore>>
    {
        private readonly ILoreRepository _repository;

        public GetLoreByWorldIdQueryHandler(ILoreRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<Lore>> HandleAsync(GetLoreByWorldIdQuery query, CancellationToken ct = default)
        {
            if (query.WorldId is not null)
                return await _repository.GetByWorldIdAsync(query.WorldId.Value, ct);
            else
                return await _repository.GetAllAsync(ct);
        }
    }

    public class GetGeneratedLoreQueryHandler : IQueryHandler<GetGeneratedLoreQuery, IEnumerable<Lore>>
    {
        private readonly ILoreRepository _repository;

        public GetGeneratedLoreQueryHandler(ILoreRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<Lore>> HandleAsync(GetGeneratedLoreQuery query, CancellationToken ct = default)
        {
            return await _repository.GetGeneratedLoreAsync(ct);
        }
    }

    public class SearchLoreByTextQueryHandler : IQueryHandler<SearchLoreByTextQuery, IEnumerable<Lore>>
    {
        private readonly ILoreRepository _repository;

        public SearchLoreByTextQueryHandler(ILoreRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<Lore>> HandleAsync(SearchLoreByTextQuery query, CancellationToken ct = default)
        {
            return await _repository.SearchByTextAsync(query.SearchTerm, ct);
        }
    }
}