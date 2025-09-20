using HotChocolate.Types;
using Npc.Api.Entities;
using Npc.Api.Data;
using Npc.Api.GraphQL.DataLoaders;
using Microsoft.EntityFrameworkCore;

namespace Npc.Api.GraphQL.Types
{
    public class CharacterType : ObjectType<Character>
    {
        protected override void Configure(IObjectTypeDescriptor<Character> descriptor)
        {
            descriptor
                .Description("Represents an NPC character in the game world.");

            descriptor
                .Field(c => c.Id)
                .Description("The unique identifier for the character.");

            descriptor
                .Field(c => c.Name)
                .Description("The character's name.");

            descriptor
                .Field(c => c.Age)
                .Description("The character's age in years.");

            descriptor
                .Field(c => c.Description)
                .Description("A detailed description of the character.");

            descriptor
                .Field(c => c.AvatarUrl)
                .Description("URL to the character's avatar image.");

            descriptor
                .Field(c => c.ImageUrl)
                .Description("URL to the character's portrait image.");

            descriptor
                .Field(c => c.IsMinor)
                .Description("Indicates if the character is under 18 years old.");

            descriptor
                .Field(c => c.CreatedAt)
                .Description("When the character was created.");

            descriptor
                .Field(c => c.UpdatedAt)
                .Description("When the character was last updated.");

            // Navigation properties
            descriptor
                .Field(c => c.World)
                .Description("The world this character belongs to.")
                .ResolveWith<CharacterResolvers>(r => r.GetWorldAsync(default!, default!, default))
                .UseDbContext<CharacterDbContext>();

            descriptor
                .Field("conversations")
                .Description("Conversations this character participates in.")
                .ResolveWith<CharacterResolvers>(r => r.GetConversationsAsync(default!, default!, default))
                .UseDbContext<CharacterDbContext>();

            descriptor
                .Field("utterances")
                .Description("All utterances spoken by this character.")
                .ResolveWith<CharacterResolvers>(r => r.GetUtterancesAsync(default!, default!, default))
                .UseDbContext<CharacterDbContext>();
        }

        private class CharacterResolvers
        {
            public async Task<World?> GetWorldAsync(
                [Parent] Character character,
                WorldByIdDataLoader worldLoader,
                CancellationToken cancellationToken)
            {
                if (character.WorldId == null) return null;

                return await worldLoader.LoadAsync(character.WorldId.Value, cancellationToken);
            }

            public async Task<IEnumerable<Conversation>> GetConversationsAsync(
                [Parent] Character character,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                return await context.Conversations
                    .Where(c => c.Utterances.Any(u => u.CharacterId == character.Id))
                    .ToListAsync(cancellationToken);
            }

            public async Task<IEnumerable<Utterance>> GetUtterancesAsync(
                [Parent] Character character,
                [ScopedService] CharacterDbContext context,
                CancellationToken cancellationToken)
            {
                return await context.Utterances
                    .Where(u => u.CharacterId == character.Id)
                    .OrderBy(u => u.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
        }
    }
}