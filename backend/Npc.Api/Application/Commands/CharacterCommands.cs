using AutoMapper;
using Npc.Api.Entities;
using Npc.Api.Dtos;
using Npc.Api.Repositories;
using Npc.Api.Services;
using Npc.Api.Infrastructure.Audit;
using Npc.Api.Domain.Events;

namespace Npc.Api.Application.Commands
{
    // Command DTOs
    public record CreateCharacterCommand(CharacterRequest Request) : ICommand<Character>;
    public record UpdateCharacterCommand(Guid Id, CharacterRequest Request) : ICommand<Character>;
    public record DeleteCharacterCommand(Guid Id) : ICommand;

    // Command Handlers
    public class CreateCharacterCommandHandler : ICommandHandler<CreateCharacterCommand, Character>
    {
        private readonly ICharacterRepository _repository;
        private readonly IModerationService _moderationService;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly IMapper _mapper;

        public CreateCharacterCommandHandler(
            ICharacterRepository repository,
            IModerationService moderationService,
            IAuditService auditService,
            IDomainEventDispatcher eventDispatcher,
            IMapper mapper)
        {
            _repository = repository;
            _moderationService = moderationService;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
            _mapper = mapper;
        }

        public async Task<Character> HandleAsync(CreateCharacterCommand command, CancellationToken ct = default)
        {
            // Business logic: Moderation check
            var advisory = await _moderationService.AnalyzeAsync(command.Request.Age, command.Request.Description, ct);

            // Use AutoMapper to convert DTO to Entity
            var character = _mapper.Map<Character>(command.Request);

            var createdCharacter = await _repository.AddAsync(character, ct);

            // Audit trail
            await _auditService.LogCharacterChangeAsync("CREATE", createdCharacter.Id, null, createdCharacter, "api-user", ct);

            // Telemetry
            Infrastructure.Observability.Telemetry.CharactersCreated.Add(1);

            // Dispatch domain event for database synchronization
            await _eventDispatcher.DispatchAsync(new CharacterCreatedEvent(createdCharacter), ct);

            return createdCharacter;
        }
    }

    public class UpdateCharacterCommandHandler : ICommandHandler<UpdateCharacterCommand, Character>
    {
        private readonly ICharacterRepository _repository;
        private readonly IModerationService _moderationService;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly IMapper _mapper;

        public UpdateCharacterCommandHandler(
            ICharacterRepository repository,
            IModerationService moderationService,
            IAuditService auditService,
            IDomainEventDispatcher eventDispatcher,
            IMapper mapper)
        {
            _repository = repository;
            _moderationService = moderationService;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
            _mapper = mapper;
        }

        public async Task<Character> HandleAsync(UpdateCharacterCommand command, CancellationToken ct = default)
        {
            var existingCharacter = await _repository.GetByIdAsync(command.Id, ct);
            if (existingCharacter == null)
                throw new InvalidOperationException($"Character with ID {command.Id} not found");

            // Business logic: Moderation check
            var advisory = await _moderationService.AnalyzeAsync(command.Request.Age, command.Request.Description, ct);

            // Capture old values for audit
            var oldCharacter = new { existingCharacter.Name, existingCharacter.Age, existingCharacter.Description, existingCharacter.AvatarUrl };

            // Use AutoMapper to update entity from DTO
            _mapper.Map(command.Request, existingCharacter);

            var updatedCharacter = await _repository.UpdateAsync(existingCharacter, ct);

            // Audit trail
            await _auditService.LogCharacterChangeAsync("UPDATE", updatedCharacter.Id, oldCharacter, updatedCharacter, "api-user", ct);

            // Dispatch domain event for database synchronization
            await _eventDispatcher.DispatchAsync(new CharacterUpdatedEvent(updatedCharacter, oldCharacter), ct);

            return updatedCharacter;
        }
    }

    public class DeleteCharacterCommandHandler : ICommandHandler<DeleteCharacterCommand>
    {
        private readonly ICharacterRepository _repository;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;

        public DeleteCharacterCommandHandler(ICharacterRepository repository, IAuditService auditService, IDomainEventDispatcher eventDispatcher)
        {
            _repository = repository;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
        }

        public async Task<Unit> HandleAsync(DeleteCharacterCommand command, CancellationToken ct = default)
        {
            var character = await _repository.GetByIdAsync(command.Id, ct);
            if (character == null)
                throw new InvalidOperationException($"Character with ID {command.Id} not found");

            // Capture entity for audit before deletion
            var deletedCharacter = new { character.Id, character.Name, character.Age, character.Description, character.AvatarUrl };

            await _repository.DeleteAsync(command.Id, ct);

            // Audit trail
            await _auditService.LogCharacterChangeAsync("DELETE", command.Id, deletedCharacter, null, "api-user", ct);

            // Dispatch domain event for database synchronization
            await _eventDispatcher.DispatchAsync(new CharacterDeletedEvent(command.Id, deletedCharacter), ct);

            return Unit.Value;
        }
    }
}