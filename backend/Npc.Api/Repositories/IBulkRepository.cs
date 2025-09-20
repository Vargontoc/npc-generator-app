namespace Npc.Api.Repositories
{
    public interface IBulkRepository<T> where T : class
    {
        Task<IEnumerable<T>> BulkAddAsync(IEnumerable<T> entities, CancellationToken ct = default);
        Task<IEnumerable<T>> BulkUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default);
        Task BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
        Task<int> BulkDeleteByConditionAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    }

    // Enhanced repository interfaces with bulk operations
    public interface ICharacterRepository : IRepository<Entities.Character>, IBulkRepository<Entities.Character>
    {
        // Character-specific methods remain the same
        Task<(IEnumerable<Entities.Character> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
        Task<IEnumerable<Entities.Character>> GetByAgeRangeAsync(int minAge, int maxAge, CancellationToken ct = default);
        Task<IEnumerable<Entities.Character>> SearchByNameAsync(string name, CancellationToken ct = default);
    }

    public interface IWorldRepository : IRepository<Entities.World>, IBulkRepository<Entities.World>
    {
        // World-specific methods remain the same
        Task<(IEnumerable<Entities.World> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
        Task<IEnumerable<Entities.World>> GetWorldsWithLoreAsync(CancellationToken ct = default);
        Task<Entities.World?> GetWorldWithLoreByIdAsync(Guid id, CancellationToken ct = default);
    }

    public interface ILoreRepository : IRepository<Entities.Lore>, IBulkRepository<Entities.Lore>
    {
        // Lore-specific methods remain the same
        Task<IEnumerable<Entities.Lore>> GetByWorldIdAsync(Guid? worldId, CancellationToken ct = default);
        Task<IEnumerable<Entities.Lore>> GetGeneratedLoreAsync(CancellationToken ct = default);
        Task<IEnumerable<Entities.Lore>> SearchByTextAsync(string searchText, CancellationToken ct = default);
    }
}