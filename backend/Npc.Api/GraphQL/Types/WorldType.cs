using HotChocolate.Types;
using Npc.Api.Entities;
using Npc.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Npc.Api.GraphQL.Types
{
    public class WorldType : ObjectType<World>
    {
        protected override void Configure(IObjectTypeDescriptor<World> descriptor)
        {
            descriptor
                .Description("Represents a game world or campaign setting.");

            descriptor
                .Field(w => w.Id)
                .Description("The unique identifier for the world.");

            descriptor
                .Field(w => w.Name)
                .Description("The name of the world.");

            descriptor
                .Field(w => w.Description)
                .Description("A detailed description of the world.");

            descriptor
                .Field(w => w.CreatedAt)
                .Description("When the world was created.");

            descriptor
                .Field(w => w.UpdatedAt)
                .Description("When the world was last updated.");

            // Navigation properties with resolvers
            descriptor
                .Field(w => w.Characters)
                .Description("All characters in this world.")
                .UseDbContext<CharacterDbContext>()
                .ResolveWith<WorldResolvers>(r => r.GetCharactersAsync(default!, default!, default));

            descriptor
                .Field(w => w.LoreEntries)
                .Description("All lore entries for this world.")
                .UseDbContext<CharacterDbContext>()
                .ResolveWith<WorldResolvers>(r => r.GetLoreEntriesAsync(default!, default!, default));

            descriptor
                .Field("conversations")
                .Description("All conversations in this world.")
                .UseDbContext<CharacterDbContext>()
                .ResolveWith<WorldResolvers>(r => r.GetConversationsAsync(default!, default!, default));

            // Computed fields
            descriptor
                .Field("characterCount")
                .Type<IntType>()
                .Description("Total number of characters in this world.")
                .UseDbContext<CharacterDbContext>()
                .ResolveWith<WorldResolvers>(r => r.GetCharacterCountAsync(default!, default!, default));

            descriptor
                .Field("loreCount")
                .Type<IntType>()
                .Description("Total number of lore entries in this world.")
                .UseDbContext<CharacterDbContext>()
                .ResolveWith<WorldResolvers>(r => r.GetLoreCountAsync(default!, default!, default));
        }

        private class WorldResolvers
        {
            public async Task<IEnumerable<Character>> GetCharactersAsync(
                [Parent] World world,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                return await context.Characters
                    .Where(c => c.WorldId == world.Id)
                    .OrderBy(c => c.Name)
                    .ToListAsync(cancellationToken);
            }

            public async Task<IEnumerable<Lore>> GetLoreEntriesAsync(
                [Parent] World world,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                return await context.LoreEntries
                    .Where(l => l.WorldId == world.Id)
                    .OrderBy(l => l.Title)
                    .ToListAsync(cancellationToken);
            }

            public async Task<IEnumerable<Conversation>> GetConversationsAsync(
                [Parent] World world,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                return await context.Conversations
                    .Where(c => c.WorldId == world.Id)
                    .OrderBy(c => c.Title)
                    .ToListAsync(cancellationToken);
            }

            public async Task<int> GetCharacterCountAsync(
                [Parent] World world,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                return await context.Characters
                    .CountAsync(c => c.WorldId == world.Id, cancellationToken);
            }

            public async Task<int> GetLoreCountAsync(
                [Parent] World world,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                return await context.LoreEntries
                    .CountAsync(l => l.WorldId == world.Id, cancellationToken);
            }
        }
    }
}