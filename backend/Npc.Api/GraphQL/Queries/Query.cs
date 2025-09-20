using HotChocolate.Types;
using HotChocolate.Data;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Entities;

namespace Npc.Api.GraphQL.Queries
{
    public class Query
    {
        // Character queries
        [UseDbContext(typeof(CharacterDbContext))]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<Character> GetCharacters([Service] CharacterDbContext context)
        {
            return context.Characters;
        }

        [UseDbContext(typeof(CharacterDbContext))]
        [UseFirstOrDefault]
        [UseProjection]
        public IQueryable<Character?> GetCharacter(Guid id, [ScopedService] CharacterDbContext context)
        {
            return context.Characters.Where(c => c.Id == id);
        }

        // World queries
        [UseDbContext(typeof(CharacterDbContext))]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<World> GetWorlds([ScopedService] CharacterDbContext context)
        {
            return context.Worlds;
        }

        [UseDbContext(typeof(CharacterDbContext))]
        [UseFirstOrDefault]
        [UseProjection]
        public IQueryable<World?> GetWorld(Guid id, [ScopedService] CharacterDbContext context)
        {
            return context.Worlds.Where(w => w.Id == id);
        }

        // Lore queries
        [UseDbContext(typeof(CharacterDbContext))]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<Lore> GetLoreEntries([ScopedService] CharacterDbContext context)
        {
            return context.LoreEntries;
        }

        [UseDbContext(typeof(CharacterDbContext))]
        [UseFirstOrDefault]
        [UseProjection]
        public IQueryable<Lore?> GetLoreEntry(Guid id, [ScopedService] CharacterDbContext context)
        {
            return context.LoreEntries.Where(l => l.Id == id);
        }

        // Conversation queries
        [UseDbContext(typeof(CharacterDbContext))]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<Conversation> GetConversations([ScopedService] CharacterDbContext context)
        {
            return context.Conversations;
        }

        [UseDbContext(typeof(CharacterDbContext))]
        [UseFirstOrDefault]
        [UseProjection]
        public IQueryable<Conversation?> GetConversation(Guid id, [ScopedService] CharacterDbContext context)
        {
            return context.Conversations.Where(c => c.Id == id);
        }

        // Utterance queries
        [UseDbContext(typeof(CharacterDbContext))]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<Utterance> GetUtterances([ScopedService] CharacterDbContext context)
        {
            return context.Utterances.Where(u => !u.Deleted);
        }

        [UseDbContext(typeof(CharacterDbContext))]
        [UseFirstOrDefault]
        [UseProjection]
        public IQueryable<Utterance?> GetUtterance(Guid id, [ScopedService] CharacterDbContext context)
        {
            return context.Utterances.Where(u => u.Id == id && !u.Deleted);
        }

        // Complex queries
        [UseDbContext(typeof(CharacterDbContext))]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public async Task<IEnumerable<Character>> GetCharactersByAge(
            int minAge,
            int maxAge,
            [ScopedService] CharacterDbContext context,
            CancellationToken cancellationToken)
        {
            return await context.Characters
                .Where(c => c.Age >= minAge && c.Age <= maxAge)
                .ToListAsync(cancellationToken);
        }

        [UseDbContext(typeof(CharacterDbContext))]
        [UseProjection]
        public async Task<IEnumerable<Lore>> GetGeneratedLore(
            [ScopedService] CharacterDbContext context,
            CancellationToken cancellationToken)
        {
            return await context.LoreEntries
                .Where(l => l.IsGenerated)
                .OrderByDescending(l => l.GeneratedAt)
                .ToListAsync(cancellationToken);
        }

        [UseDbContext(typeof(CharacterDbContext))]
        [UseProjection]
        public async Task<IEnumerable<Character>> SearchCharacters(
            string searchTerm,
            [ScopedService] CharacterDbContext context,
            CancellationToken cancellationToken)
        {
            return await context.Characters
                .Where(c => c.Name.Contains(searchTerm) ||
                           (c.Description != null && c.Description.Contains(searchTerm)))
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        [UseDbContext(typeof(CharacterDbContext))]
        [UseProjection]
        public async Task<IEnumerable<Conversation>> GetConversationsByCharacter(
            Guid characterId,
            [ScopedService] CharacterDbContext context,
            CancellationToken cancellationToken)
        {
            return await context.Conversations
                .Where(c => c.Utterances.Any(u => u.CharacterId == characterId && !u.Deleted))
                .OrderBy(c => c.Title)
                .ToListAsync(cancellationToken);
        }

        // Statistics queries
        public async Task<WorldStatistics> GetWorldStatistics(
            Guid worldId,
            [ScopedService] CharacterDbContext context,
            CancellationToken cancellationToken)
        {
            var world = await context.Worlds.FirstOrDefaultAsync(w => w.Id == worldId, cancellationToken);
            if (world == null) throw new GraphQLException("World not found");

            var characterCount = await context.Characters.CountAsync(c => c.WorldId == worldId, cancellationToken);
            var loreCount = await context.LoreEntries.CountAsync(l => l.WorldId == worldId, cancellationToken);
            var conversationCount = await context.Conversations.CountAsync(c => c.WorldId == worldId, cancellationToken);

            var averageCharacterAge = characterCount > 0
                ? await context.Characters.Where(c => c.WorldId == worldId).AverageAsync(c => c.Age, cancellationToken)
                : 0;

            return new WorldStatistics
            {
                WorldId = worldId,
                WorldName = world.Name,
                CharacterCount = characterCount,
                LoreCount = loreCount,
                ConversationCount = conversationCount,
                AverageCharacterAge = averageCharacterAge
            };
        }
    }

    public class WorldStatistics
    {
        public Guid WorldId { get; set; }
        public string WorldName { get; set; } = string.Empty;
        public int CharacterCount { get; set; }
        public int LoreCount { get; set; }
        public int ConversationCount { get; set; }
        public double AverageCharacterAge { get; set; }
    }
}