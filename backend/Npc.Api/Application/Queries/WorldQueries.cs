using Npc.Api.Entities;
using Npc.Api.Repositories;

namespace Npc.Api.Application.Queries
{
    // Query DTOs
    public record GetWorldByIdQuery(Guid Id) : IQuery<World?>;
    public record GetWorldsPagedQuery(int Page, int PageSize) : IQuery<(IEnumerable<World> Items, int TotalCount)>;
    public record GetWorldsWithLoreQuery() : IQuery<IEnumerable<World>>;
    public record GetWorldWithLoreByIdQuery(Guid Id) : IQuery<World?>;

    // Query Handlers
    public class GetWorldByIdQueryHandler : IQueryHandler<GetWorldByIdQuery, World?>
    {
        private readonly IWorldRepository _repository;

        public GetWorldByIdQueryHandler(IWorldRepository repository)
        {
            _repository = repository;
        }

        public async Task<World?> HandleAsync(GetWorldByIdQuery query, CancellationToken ct = default)
        {
            return await _repository.GetByIdAsync(query.Id, ct);
        }
    }

    public class GetWorldsPagedQueryHandler : IQueryHandler<GetWorldsPagedQuery, (IEnumerable<World> Items, int TotalCount)>
    {
        private readonly IWorldRepository _repository;

        public GetWorldsPagedQueryHandler(IWorldRepository repository)
        {
            _repository = repository;
        }

        public async Task<(IEnumerable<World> Items, int TotalCount)> HandleAsync(GetWorldsPagedQuery query, CancellationToken ct = default)
        {
            return await _repository.GetPagedAsync(query.Page, query.PageSize, ct);
        }
    }

    public class GetWorldsWithLoreQueryHandler : IQueryHandler<GetWorldsWithLoreQuery, IEnumerable<World>>
    {
        private readonly IWorldRepository _repository;

        public GetWorldsWithLoreQueryHandler(IWorldRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<World>> HandleAsync(GetWorldsWithLoreQuery query, CancellationToken ct = default)
        {
            return await _repository.GetWithLoreAsync(ct);
        }
    }

    public class GetWorldWithLoreByIdQueryHandler : IQueryHandler<GetWorldWithLoreByIdQuery, World?>
    {
        private readonly IWorldRepository _repository;

        public GetWorldWithLoreByIdQueryHandler(IWorldRepository repository)
        {
            _repository = repository;
        }

        public async Task<World?> HandleAsync(GetWorldWithLoreByIdQuery query, CancellationToken ct = default)
        {
            return await _repository.GetWithLoreByIdAsync(query.Id, ct);
        }
    }
}