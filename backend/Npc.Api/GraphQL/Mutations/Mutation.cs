using HotChocolate.Types;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Entities;
using Npc.Api.GraphQL.InputTypes;
using Npc.Api.Services;

namespace Npc.Api.GraphQL.Mutations
{
    public class Mutation
    {
        // Character mutations
        [UseDbContext(typeof(CharacterDbContext))]
        public async Task<Character> CreateCharacter(
            CreateCharacterInput input,
            [ScopedService] CharacterDbContext context,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            var character = new Character
            {
                Name = input.Name,
                Age = input.Age,
                Description = input.Description,
                WorldId = input.WorldId
            };

            context.Characters.Add(character);
            await context.SaveChangesAsync(cancellationToken);

            // Send subscription event
            await eventSender.SendAsync("CharacterCreated", character, cancellationToken);

            return character;
        }

        [UseDbContext(typeof(CharacterDbContext))]
        public async Task<Character> UpdateCharacter(
            Guid id,
            UpdateCharacterInput input,
            [ScopedService] CharacterDbContext context,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            var character = await context.Characters.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (character == null) throw new GraphQLException("Character not found");

            if (input.Name != null) character.Name = input.Name;
            if (input.Age.HasValue) character.Age = input.Age.Value;
            if (input.Description != null) character.Description = input.Description;
            if (input.WorldId.HasValue) character.WorldId = input.WorldId;

            await context.SaveChangesAsync(cancellationToken);

            // Send subscription event
            await eventSender.SendAsync("CharacterUpdated", character, cancellationToken);

            return character;
        }

        [UseDbContext(typeof(CharacterDbContext))]
        public async Task<bool> DeleteCharacter(
            Guid id,
            [ScopedService] CharacterDbContext context,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            var character = await context.Characters.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (character == null) return false;

            context.Characters.Remove(character);
            await context.SaveChangesAsync(cancellationToken);

            // Send subscription event
            await eventSender.SendAsync("CharacterDeleted", id, cancellationToken);

            return true;
        }

        // World mutations
        [UseDbContext(typeof(CharacterDbContext))]
        public async Task<World> CreateWorld(
            CreateWorldInput input,
            [ScopedService] CharacterDbContext context,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            var world = new World
            {
                Name = input.Name,
                Description = input.Description
            };

            context.Worlds.Add(world);
            await context.SaveChangesAsync(cancellationToken);

            // Send subscription event
            await eventSender.SendAsync("WorldCreated", world, cancellationToken);

            return world;
        }

        [UseDbContext(typeof(CharacterDbContext))]
        public async Task<World> UpdateWorld(
            Guid id,
            UpdateWorldInput input,
            [ScopedService] CharacterDbContext context,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            var world = await context.Worlds.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
            if (world == null) throw new GraphQLException("World not found");

            if (input.Name != null) world.Name = input.Name;
            if (input.Description != null) world.Description = input.Description;

            await context.SaveChangesAsync(cancellationToken);

            // Send subscription event
            await eventSender.SendAsync("WorldUpdated", world, cancellationToken);

            return world;
        }

        // Lore mutations
        [UseDbContext(typeof(CharacterDbContext))]
        public async Task<Lore> CreateLore(
            CreateLoreInput input,
            [ScopedService] CharacterDbContext context,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            var lore = new Lore
            {
                Title = input.Title,
                Text = input.Text,
                WorldId = input.WorldId,
                IsGenerated = input.IsGenerated,
                GenerationSource = input.GenerationSource,
                GenerationMeta = input.GenerationMeta
            };

            if (input.IsGenerated)
            {
                lore.GeneratedAt = DateTimeOffset.UtcNow;
            }

            context.LoreEntries.Add(lore);
            await context.SaveChangesAsync(cancellationToken);

            // Send subscription event
            await eventSender.SendAsync("LoreCreated", lore, cancellationToken);

            return lore;
        }

        // Conversation mutations
        [UseDbContext(typeof(CharacterDbContext))]
        public async Task<Conversation> CreateConversation(
            CreateConversationInput input,
            [ScopedService] CharacterDbContext context,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            var conversation = new Conversation
            {
                Title = input.Title,
                WorldId = input.WorldId
            };

            context.Conversations.Add(conversation);
            await context.SaveChangesAsync(cancellationToken);

            // Send subscription event
            await eventSender.SendAsync("ConversationCreated", conversation, cancellationToken);

            return conversation;
        }

        [UseDbContext(typeof(CharacterDbContext))]
        public async Task<Utterance> AddUtterance(
            AddUtteranceInput input,
            [ScopedService] CharacterDbContext context,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            var utterance = new Utterance
            {
                Text = input.Text,
                ConversationId = input.ConversationId,
                CharacterId = input.CharacterId,
                Tags = input.Tags ?? Array.Empty<string>()
            };

            context.Utterances.Add(utterance);
            await context.SaveChangesAsync(cancellationToken);

            // Send subscription event
            await eventSender.SendAsync("UtteranceAdded", utterance, cancellationToken);

            return utterance;
        }

        // AI-powered mutations
        [UseDbContext(typeof(CharacterDbContext))]
        public async Task<Character> GenerateCharacterWithAI(
            GenerateCharacterInput input,
            [ScopedService] CharacterDbContext context,
            [Service] IAgentConversationService agentService,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            // This would integrate with your existing AI generation service
            // For now, creating a placeholder implementation
            var character = new Character
            {
                Name = $"Generated Character {DateTime.Now:HHmmss}",
                Age = Random.Shared.Next(18, 80),
                Description = "AI-generated character description",
                WorldId = input.WorldId
            };

            context.Characters.Add(character);
            await context.SaveChangesAsync(cancellationToken);

            // Send subscription event
            await eventSender.SendAsync("CharacterGenerated", character, cancellationToken);

            return character;
        }

        [UseDbContext(typeof(CharacterDbContext))]
        public async Task<Lore> GenerateLoreWithAI(
            GenerateLoreInput input,
            [ScopedService] CharacterDbContext context,
            [Service] IAgentConversationService agentService,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            // This would integrate with your existing AI generation service
            var lore = new Lore
            {
                Title = $"Generated Lore: {input.Topic}",
                Text = $"AI-generated lore content about {input.Topic}...",
                WorldId = input.WorldId,
                IsGenerated = true,
                GenerationSource = "agent",
                GenerationMeta = $"{{\"topic\": \"{input.Topic}\", \"style\": \"{input.Style}\"}}",
                GeneratedAt = DateTimeOffset.UtcNow
            };

            context.LoreEntries.Add(lore);
            await context.SaveChangesAsync(cancellationToken);

            // Send subscription event
            await eventSender.SendAsync("LoreGenerated", lore, cancellationToken);

            return lore;
        }
    }
}