using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Entities;

namespace Npc.Api.GraphQL.DataLoaders
{
    public class LoreByIdDataLoader : BatchDataLoader<Guid, Lore>
    {
        private readonly IDbContextFactory<CharacterDbContext> _dbContextFactory;

        public LoreByIdDataLoader(
            IDbContextFactory<CharacterDbContext> dbContextFactory,
            IBatchScheduler batchScheduler,
            DataLoaderOptions? options = null)
            : base(batchScheduler, options)
        {
            _dbContextFactory = dbContextFactory;
        }

        protected override async Task<IReadOnlyDictionary<Guid, Lore>> LoadBatchAsync(
            IReadOnlyList<Guid> keys,
            CancellationToken cancellationToken)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var loreEntries = await context.LoreEntries
                .Where(l => keys.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken);

            return loreEntries;
        }
    }

    public class LoreByWorldDataLoader : GroupedDataLoader<Guid, Lore>
    {
        private readonly IDbContextFactory<CharacterDbContext> _dbContextFactory;

        public LoreByWorldDataLoader(
            IDbContextFactory<CharacterDbContext> dbContextFactory,
            IBatchScheduler batchScheduler,
            DataLoaderOptions? options = null)
            : base(batchScheduler, options)
        {
            _dbContextFactory = dbContextFactory;
        }

        protected override async Task<ILookup<Guid, Lore>> LoadGroupedBatchAsync(
            IReadOnlyList<Guid> keys,
            CancellationToken cancellationToken)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var loreEntries = await context.LoreEntries
                .Where(l => l.WorldId.HasValue && keys.Contains(l.WorldId.Value))
                .ToListAsync(cancellationToken);

            return loreEntries.ToLookup(l => l.WorldId!.Value);
        }
    }
}