using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Entities;

namespace Npc.Api.Repositories
{
    public abstract class PostgresRepository<T> : IRepository<T>, IBulkRepository<T> where T : BaseEntity
    {
        protected readonly CharacterDbContext _context;
        protected readonly DbSet<T> _dbSet;

        protected PostgresRepository(CharacterDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _dbSet.FirstOrDefaultAsync(e => e.Id == id, ct);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking().ToListAsync(ct);
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking().Where(predicate).ToListAsync(ct);
        }

        public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
        {
            _dbSet.Add(entity);
            await _context.SaveChangesAsync(ct);
            return entity;
        }

        public virtual async Task<T> UpdateAsync(T entity, CancellationToken ct = default)
        {
            _dbSet.Update(entity);
            await _context.SaveChangesAsync(ct);
            return entity;
        }

        public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await GetByIdAsync(id, ct);
            if (entity != null)
            {
                _dbSet.Remove(entity);
                await _context.SaveChangesAsync(ct);
            }
        }

        public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        {
            return await _dbSet.AnyAsync(e => e.Id == id, ct);
        }

        public virtual async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

            var totalCount = await _dbSet.CountAsync(ct);
            var items = await _dbSet
                .AsNoTracking()
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }

        // Bulk Operations Implementation
        public virtual async Task<IEnumerable<T>> BulkAddAsync(IEnumerable<T> entities, CancellationToken ct = default)
        {
            var entitiesArray = entities.ToArray();

            foreach (var entity in entitiesArray)
            {
                entity.CreatedAt = DateTimeOffset.UtcNow;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _dbSet.AddRangeAsync(entitiesArray, ct);
            await _context.SaveChangesAsync(ct);

            return entitiesArray;
        }

        public virtual async Task<IEnumerable<T>> BulkUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default)
        {
            var entitiesArray = entities.ToArray();

            foreach (var entity in entitiesArray)
            {
                entity.UpdatedAt = DateTimeOffset.UtcNow;
            }

            _dbSet.UpdateRange(entitiesArray);
            await _context.SaveChangesAsync(ct);

            return entitiesArray;
        }

        public virtual async Task BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            var idsArray = ids.ToArray();
            var entities = await _dbSet
                .Where(e => idsArray.Contains(e.Id))
                .ToListAsync(ct);

            if (entities.Any())
            {
                _dbSet.RemoveRange(entities);
                await _context.SaveChangesAsync(ct);
            }
        }

        public virtual async Task<int> BulkDeleteByConditionAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            var entities = await _dbSet.Where(predicate).ToListAsync(ct);

            if (entities.Any())
            {
                _dbSet.RemoveRange(entities);
                await _context.SaveChangesAsync(ct);
            }

            return entities.Count;
        }
    }

    public class CharacterRepository : PostgresRepository<Character>, ICharacterRepository
    {
        public CharacterRepository(CharacterDbContext context) : base(context) { }

        public async Task<IEnumerable<Character>> GetByAgeRangeAsync(int minAge, int maxAge, CancellationToken ct = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(c => c.Age >= minAge && c.Age <= maxAge)
                .OrderBy(c => c.Age)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<Character>> SearchByNameAsync(string namePattern, CancellationToken ct = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(c => EF.Functions.ILike(c.Name, $"%{namePattern}%"))
                .OrderBy(c => c.Name)
                .ToListAsync(ct);
        }
    }

    public class WorldRepository : PostgresRepository<World>, IWorldRepository
    {
        public WorldRepository(CharacterDbContext context) : base(context) { }

        public async Task<IEnumerable<World>> GetWorldsWithLoreAsync(CancellationToken ct = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Include(w => w.LoreEntries)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<World?> GetWorldWithLoreByIdAsync(Guid worldId, CancellationToken ct = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Include(w => w.LoreEntries)
                .FirstOrDefaultAsync(w => w.Id == worldId, ct);
        }
    }

    public class LoreRepository : PostgresRepository<Lore>, ILoreRepository
    {
        public LoreRepository(CharacterDbContext context) : base(context) { }

        public async Task<IEnumerable<Lore>> GetByWorldIdAsync(Guid? worldId, CancellationToken ct = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(l => l.WorldId == worldId)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<Lore>> GetGeneratedLoreAsync(CancellationToken ct = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(l => l.IsGenerated)
                .OrderByDescending(l => l.GeneratedAt)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<Lore>> SearchByTextAsync(string searchTerm, CancellationToken ct = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(l => EF.Functions.ILike(l.Title, $"%{searchTerm}%") ||
                           (l.Text != null && EF.Functions.ILike(l.Text, $"%{searchTerm}%")))
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync(ct);
        }
    }
}