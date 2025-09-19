using System.Linq.Expressions;

namespace Npc.Api.Repositories
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
        Task<T> AddAsync(T entity, CancellationToken ct = default);
        Task<T> UpdateAsync(T entity, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
        Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    }

    public interface ICharacterRepository : IRepository<Entities.Character>
    {
        Task<IEnumerable<Entities.Character>> GetByAgeRangeAsync(int minAge, int maxAge, CancellationToken ct = default);
        Task<IEnumerable<Entities.Character>> SearchByNameAsync(string namePattern, CancellationToken ct = default);
    }

    public interface IWorldRepository : IRepository<Entities.World>
    {
        Task<IEnumerable<Entities.World>> GetWithLoreAsync(CancellationToken ct = default);
        Task<Entities.World?> GetWithLoreByIdAsync(Guid worldId, CancellationToken ct = default);
    }

    public interface ILoreRepository : IRepository<Entities.Lore>
    {
        Task<IEnumerable<Entities.Lore>> GetByWorldIdAsync(Guid worldId, CancellationToken ct = default);
        Task<IEnumerable<Entities.Lore>> GetGeneratedLoreAsync(CancellationToken ct = default);
        Task<IEnumerable<Entities.Lore>> SearchByTextAsync(string searchTerm, CancellationToken ct = default);
    }
}