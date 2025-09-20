using HotChocolate.Types;
using Npc.Api.Entities;
using Npc.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Npc.Api.GraphQL.Types
{
    public class UtteranceType : ObjectType<Utterance>
    {
        protected override void Configure(IObjectTypeDescriptor<Utterance> descriptor)
        {
            descriptor
                .Description("Represents a single utterance or line of dialogue.");

            descriptor
                .Field(u => u.Id)
                .Description("The unique identifier for the utterance.");

            descriptor
                .Field(u => u.Text)
                .Description("The text content of the utterance.");

            descriptor
                .Field(u => u.Version)
                .Description("Version number of this utterance.");

            descriptor
                .Field(u => u.Deleted)
                .Description("Whether this utterance has been soft-deleted.");

            descriptor
                .Field(u => u.Tags)
                .Description("Tags associated with this utterance.");

            descriptor
                .Field(u => u.CreatedAt)
                .Description("When the utterance was created.");

            descriptor
                .Field(u => u.UpdatedAt)
                .Description("When the utterance was last updated.");

            // Navigation properties
            descriptor
                .Field(u => u.Character)
                .Description("The character who spoke this utterance.")
                .ResolveWith<UtteranceResolvers>(r => r.GetCharacterAsync(default!, default!, default))
                .UseDbContext<CharacterDbContext>();

            descriptor
                .Field(u => u.Conversation)
                .Description("The conversation this utterance belongs to.")
                .ResolveWith<UtteranceResolvers>(r => r.GetConversationAsync(default!, default!, default))
                .UseDbContext<CharacterDbContext>();

            // Computed fields
            descriptor
                .Field("wordCount")
                .Type<IntType>()
                .Description("Number of words in this utterance.")
                .ResolveWith<UtteranceResolvers>(r => r.GetWordCount(default!));

            descriptor
                .Field("characterCount")
                .Type<IntType>()
                .Description("Number of characters in this utterance.")
                .ResolveWith<UtteranceResolvers>(r => r.GetCharacterCount(default!));

            descriptor
                .Field("isNarration")
                .Type<BooleanType>()
                .Description("Whether this utterance is narration (no character assigned).")
                .ResolveWith<UtteranceResolvers>(r => r.IsNarration(default!));
        }

        private class UtteranceResolvers
        {
            public async Task<Character?> GetCharacterAsync(
                [Parent] Utterance utterance,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                if (utterance.CharacterId == null) return null;

                return await context.Characters
                    .FirstOrDefaultAsync(c => c.Id == utterance.CharacterId, cancellationToken);
            }

            public async Task<Conversation?> GetConversationAsync(
                [Parent] Utterance utterance,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                return await context.Conversations
                    .FirstOrDefaultAsync(c => c.Id == utterance.ConversationId, cancellationToken);
            }

            public int GetWordCount([Parent] Utterance utterance)
            {
                if (string.IsNullOrWhiteSpace(utterance.Text))
                    return 0;

                return utterance.Text
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Length;
            }

            public int GetCharacterCount([Parent] Utterance utterance)
            {
                return utterance.Text?.Length ?? 0;
            }

            public bool IsNarration([Parent] Utterance utterance)
            {
                return utterance.CharacterId == null;
            }
        }
    }
}