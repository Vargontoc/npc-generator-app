using Npc.Api.Dtos;
using Npc.Api.Repositories;

namespace Npc.Api.Application.Commands
{
    // Command DTOs - Main conversation operations
    public record CreateConversationCommand(string Title) : ICommand<ConversationResponse>;
    public record AddRootUtteranceCommand(Guid ConversationId, string Text, Guid? CharacterId) : ICommand<UtteranceResponse>;
    public record AddNextUtteranceCommand(Guid FromUtteranceId, string Text, Guid? CharacterId) : ICommand<UtteranceResponse>;
    public record UpdateUtteranceCommand(Guid UtteranceId, string Text, string[]? Tags, int Version) : ICommand<UtteranceDetail?>;
    public record DeleteUtteranceCommand(Guid UtteranceId) : ICommand<bool>;
    public record AddBranchCommand(Guid FromUtteranceId, Guid ToUtteranceId, double? Weight) : ICommand;

    // Command Handlers
    public class CreateConversationCommandHandler : ICommandHandler<CreateConversationCommand, ConversationResponse>
    {
        private readonly IConversationRepository _repository;

        public CreateConversationCommandHandler(IConversationRepository repository)
        {
            _repository = repository;
        }

        public async Task<ConversationResponse> HandleAsync(CreateConversationCommand command, CancellationToken ct = default)
        {
            return await _repository.CreateConversationAsync(command.Title, ct);
        }
    }

    public class AddRootUtteranceCommandHandler : ICommandHandler<AddRootUtteranceCommand, UtteranceResponse>
    {
        private readonly IConversationRepository _repository;

        public AddRootUtteranceCommandHandler(IConversationRepository repository)
        {
            _repository = repository;
        }

        public async Task<UtteranceResponse> HandleAsync(AddRootUtteranceCommand command, CancellationToken ct = default)
        {
            return await _repository.AddRootUtteranceAsync(command.ConversationId, command.Text, command.CharacterId, ct);
        }
    }

    public class AddNextUtteranceCommandHandler : ICommandHandler<AddNextUtteranceCommand, UtteranceResponse>
    {
        private readonly IConversationRepository _repository;

        public AddNextUtteranceCommandHandler(IConversationRepository repository)
        {
            _repository = repository;
        }

        public async Task<UtteranceResponse> HandleAsync(AddNextUtteranceCommand command, CancellationToken ct = default)
        {
            return await _repository.AddNextUtteranceAsync(command.FromUtteranceId, command.Text, command.CharacterId, ct);
        }
    }

    public class UpdateUtteranceCommandHandler : ICommandHandler<UpdateUtteranceCommand, UtteranceDetail?>
    {
        private readonly IConversationRepository _repository;

        public UpdateUtteranceCommandHandler(IConversationRepository repository)
        {
            _repository = repository;
        }

        public async Task<UtteranceDetail?> HandleAsync(UpdateUtteranceCommand command, CancellationToken ct = default)
        {
            return await _repository.UpdateUtteranceAsync(command.UtteranceId, command.Text, command.Tags, command.Version, ct);
        }
    }

    public class DeleteUtteranceCommandHandler : ICommandHandler<DeleteUtteranceCommand, bool>
    {
        private readonly IConversationRepository _repository;

        public DeleteUtteranceCommandHandler(IConversationRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> HandleAsync(DeleteUtteranceCommand command, CancellationToken ct = default)
        {
            return await _repository.DeleteUtteranceAsync(command.UtteranceId, ct);
        }
    }

    public class AddBranchCommandHandler : ICommandHandler<AddBranchCommand>
    {
        private readonly IConversationRepository _repository;

        public AddBranchCommandHandler(IConversationRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> HandleAsync(AddBranchCommand command, CancellationToken ct = default)
        {
            await _repository.AddBranchAsync(command.FromUtteranceId, command.ToUtteranceId, ct);

            if (command.Weight is not null)
                await _repository.SetBranchWeightAsync(command.FromUtteranceId, command.ToUtteranceId, command.Weight.Value, ct);

            return Unit.Value;
        }
    }
}