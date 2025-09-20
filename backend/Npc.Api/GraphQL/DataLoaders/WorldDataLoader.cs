using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Entities;

namespace Npc.Api.GraphQL.DataLoaders
{
    public class WorldByIdDataLoader : BatchDataLoader<Guid, World>
    {
        private readonly IDbContextFactory<CharacterDbContext> _dbContextFactory;

        public WorldByIdDataLoader(
            IDbContextFactory<CharacterDbContext> dbContextFactory,
            IBatchScheduler batchScheduler,
            DataLoaderOptions? options = null)
            : base(batchScheduler, options)
        {
            _dbContextFactory = dbContextFactory;
        }

        protected override async Task<IReadOnlyDictionary<Guid, World>> LoadBatchAsync(
            IReadOnlyList<Guid> keys,
            CancellationToken cancellationToken)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var worlds = await context.Worlds
                .Where(w => keys.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id, cancellationToken);

            return worlds;
        }
    }
}