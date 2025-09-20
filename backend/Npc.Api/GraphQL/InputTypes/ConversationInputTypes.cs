namespace Npc.Api.GraphQL.InputTypes
{
    public record CreateConversationInput(
        string Title,
        Guid? WorldId
    );

    public record UpdateConversationInput(
        string? Title,
        Guid? WorldId
    );

    public record AddUtteranceInput(
        string Text,
        Guid ConversationId,
        Guid? CharacterId,
        string[]? Tags
    );

    public record UpdateUtteranceInput(
        string? Text,
        Guid? CharacterId,
        string[]? Tags
    );
}