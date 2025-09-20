using HotChocolate.Types;
using Npc.Api.Entities;
using Npc.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Npc.Api.GraphQL.Types
{
    public class ConversationType : ObjectType<Conversation>
    {
        protected override void Configure(IObjectTypeDescriptor<Conversation> descriptor)
        {
            descriptor
                .Description("Represents a conversation or dialogue sequence.");

            descriptor
                .Field(c => c.Id)
                .Description("The unique identifier for the conversation.");

            descriptor
                .Field(c => c.Title)
                .Description("The title of the conversation.");

            descriptor
                .Field(c => c.CreatedAt)
                .Description("When the conversation was created.");

            descriptor
                .Field(c => c.UpdatedAt)
                .Description("When the conversation was last updated.");

            // Navigation properties
            descriptor
                .Field(c => c.World)
                .Description("The world this conversation belongs to.")
                .ResolveWith<ConversationResolvers>(r => r.GetWorldAsync(default!, default!, default))
                .UseDbContext<CharacterDbContext>();

            descriptor
                .Field(c => c.Utterances)
                .Description("All utterances in this conversation.")
                .UseDbContext<CharacterDbContext>()
                .ResolveWith<ConversationResolvers>(r => r.GetUtterancesAsync(default!, default!, default));

            // Computed fields
            descriptor
                .Field("participants")
                .Description("Characters that participate in this conversation.")
                .UseDbContext<CharacterDbContext>()
                .ResolveWith<ConversationResolvers>(r => r.GetParticipantsAsync(default!, default!, default));

            descriptor
                .Field("utteranceCount")
                .Type<IntType>()
                .Description("Total number of utterances in this conversation.")
                .UseDbContext<CharacterDbContext>()
                .ResolveWith<ConversationResolvers>(r => r.GetUtteranceCountAsync(default!, default!, default));

            descriptor
                .Field("totalWordCount")
                .Type<IntType>()
                .Description("Total word count of all utterances.")
                .UseDbContext<CharacterDbContext>()
                .ResolveWith<ConversationResolvers>(r => r.GetTotalWordCountAsync(default!, default!, default));
        }

        private class ConversationResolvers
        {
            public async Task<World?> GetWorldAsync(
                [Parent] Conversation conversation,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                if (conversation.WorldId == null) return null;

                return await context.Worlds
                    .FirstOrDefaultAsync(w => w.Id == conversation.WorldId, cancellationToken);
            }

            public async Task<IEnumerable<Utterance>> GetUtterancesAsync(
                [Parent] Conversation conversation,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                return await context.Utterances
                    .Where(u => u.ConversationId == conversation.Id && !u.Deleted)
                    .OrderBy(u => u.CreatedAt)
                    .ToListAsync(cancellationToken);
            }

            public async Task<IEnumerable<Character>> GetParticipantsAsync(
                [Parent] Conversation conversation,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                return await context.Characters
                    .Where(c => context.Utterances
                        .Any(u => u.ConversationId == conversation.Id && u.CharacterId == c.Id && !u.Deleted))
                    .Distinct()
                    .OrderBy(c => c.Name)
                    .ToListAsync(cancellationToken);
            }

            public async Task<int> GetUtteranceCountAsync(
                [Parent] Conversation conversation,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                return await context.Utterances
                    .CountAsync(u => u.ConversationId == conversation.Id && !u.Deleted, cancellationToken);
            }

            public async Task<int> GetTotalWordCountAsync(
                [Parent] Conversation conversation,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                var utterances = await context.Utterances
                    .Where(u => u.ConversationId == conversation.Id && !u.Deleted)
                    .Select(u => u.Text)
                    .ToListAsync(cancellationToken);

                return utterances
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Sum(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            }
        }
    }
}