using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Entities;

namespace Npc.Api.GraphQL.DataLoaders
{
    public class CharacterByIdDataLoader : BatchDataLoader<Guid, Character>
    {
        private readonly IDbContextFactory<CharacterDbContext> _dbContextFactory;

        public CharacterByIdDataLoader(
            IDbContextFactory<CharacterDbContext> dbContextFactory,
            IBatchScheduler batchScheduler,
            DataLoaderOptions? options = null)
            : base(batchScheduler, options)
        {
            _dbContextFactory = dbContextFactory;
        }

        protected override async Task<IReadOnlyDictionary<Guid, Character>> LoadBatchAsync(
            IReadOnlyList<Guid> keys,
            CancellationToken cancellationToken)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var characters = await context.Characters
                .Where(c => keys.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, cancellationToken);

            return characters;
        }
    }

    public class CharactersByWorldDataLoader : GroupedDataLoader<Guid, Character>
    {
        private readonly IDbContextFactory<CharacterDbContext> _dbContextFactory;

        public CharactersByWorldDataLoader(
            IDbContextFactory<CharacterDbContext> dbContextFactory,
            IBatchScheduler batchScheduler,
            DataLoaderOptions? options = null)
            : base(batchScheduler, options)
        {
            _dbContextFactory = dbContextFactory;
        }

        protected override async Task<ILookup<Guid, Character>> LoadGroupedBatchAsync(
            IReadOnlyList<Guid> keys,
            CancellationToken cancellationToken)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var characters = await context.Characters
                .Where(c => c.WorldId.HasValue && keys.Contains(c.WorldId.Value))
                .ToListAsync(cancellationToken);

            return characters.ToLookup(c => c.WorldId!.Value);
        }
    }
}