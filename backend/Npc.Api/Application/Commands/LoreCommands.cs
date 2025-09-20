using AutoMapper;
using Npc.Api.Entities;
using Npc.Api.Repositories;
using Npc.Api.Infrastructure.Audit;
using Npc.Api.Services;
using Npc.Api.Dtos;
using Npc.Api.Domain.Events;

namespace Npc.Api.Application.Commands
{
    // Command DTOs
    public record CreateLoreCommand(LoreRequest Request) : ICommand<Lore>;
    public record UpdateLoreCommand(Guid Id, LoreRequest Request) : ICommand<Lore>;
    public record DeleteLoreCommand(Guid Id) : ICommand;
    public record SuggestLoreCommand(Guid? WorldId, string Prompt, int Count, bool DryRun) : ICommand<LoreSuggestResponse>;

    // Command Handlers
    public class CreateLoreCommandHandler : ICommandHandler<CreateLoreCommand, Lore>
    {
        private readonly ILoreRepository _repository;
        private readonly IWorldRepository _worldRepository;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly IMapper _mapper;

        public CreateLoreCommandHandler(ILoreRepository repository, IWorldRepository worldRepository, IAuditService auditService, IDomainEventDispatcher eventDispatcher, IMapper mapper)
        {
            _repository = repository;
            _worldRepository = worldRepository;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
            _mapper = mapper;
        }

        public async Task<Lore> HandleAsync(CreateLoreCommand command, CancellationToken ct = default)
        {
            // Business logic: Validate world exists if provided
            if (command.Request.WorldId is not null)
            {
                var worldExists = await _worldRepository.ExistsAsync(command.Request.WorldId.Value, ct);
                if (!worldExists)
                    throw new InvalidOperationException($"World with ID {command.Request.WorldId} not found");
            }

            // Use AutoMapper to convert DTO to Entity
            var lore = _mapper.Map<Lore>(command.Request);

            var createdLore = await _repository.AddAsync(lore, ct);

            // Audit trail
            await _auditService.LogLoreChangeAsync("CREATE", createdLore.Id, null, createdLore, "api-user", ct);

            // Dispatch domain event for database synchronization
            await _eventDispatcher.DispatchAsync(new LoreCreatedEvent(createdLore), ct);

            return createdLore;
        }
    }

    public class UpdateLoreCommandHandler : ICommandHandler<UpdateLoreCommand, Lore>
    {
        private readonly ILoreRepository _repository;
        private readonly IWorldRepository _worldRepository;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly IMapper _mapper;

        public UpdateLoreCommandHandler(ILoreRepository repository, IWorldRepository worldRepository, IAuditService auditService, IDomainEventDispatcher eventDispatcher, IMapper mapper)
        {
            _repository = repository;
            _worldRepository = worldRepository;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
            _mapper = mapper;
        }

        public async Task<Lore> HandleAsync(UpdateLoreCommand command, CancellationToken ct = default)
        {
            var existingLore = await _repository.GetByIdAsync(command.Id, ct);
            if (existingLore == null)
                throw new InvalidOperationException($"Lore with ID {command.Id} not found");

            // Business logic: Validate world exists if provided
            if (command.Request.WorldId is not null)
            {
                var worldExists = await _worldRepository.ExistsAsync(command.Request.WorldId.Value, ct);
                if (!worldExists)
                    throw new InvalidOperationException($"World with ID {command.Request.WorldId} not found");
            }

            // Capture old values for audit
            var oldLore = new { existingLore.Title, existingLore.Text, existingLore.WorldId };

            // Use AutoMapper to update entity from DTO
            _mapper.Map(command.Request, existingLore);

            var updatedLore = await _repository.UpdateAsync(existingLore, ct);

            // Audit trail
            await _auditService.LogLoreChangeAsync("UPDATE", updatedLore.Id, oldLore, updatedLore, "api-user", ct);

            // Dispatch domain event for database synchronization
            await _eventDispatcher.DispatchAsync(new LoreUpdatedEvent(updatedLore, oldLore), ct);

            return updatedLore;
        }
    }

    public class DeleteLoreCommandHandler : ICommandHandler<DeleteLoreCommand>
    {
        private readonly ILoreRepository _repository;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;

        public DeleteLoreCommandHandler(ILoreRepository repository, IAuditService auditService, IDomainEventDispatcher eventDispatcher)
        {
            _repository = repository;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
        }

        public async Task<Unit> HandleAsync(DeleteLoreCommand command, CancellationToken ct = default)
        {
            var lore = await _repository.GetByIdAsync(command.Id, ct);
            if (lore == null)
                throw new InvalidOperationException($"Lore with ID {command.Id} not found");

            // Capture entity for audit before deletion
            var deletedLore = new { lore.Id, lore.Title, lore.Text, lore.WorldId };

            await _repository.DeleteAsync(command.Id, ct);

            // Audit trail
            await _auditService.LogLoreChangeAsync("DELETE", command.Id, deletedLore, null, "api-user", ct);

            // Dispatch domain event for database synchronization
            await _eventDispatcher.DispatchAsync(new LoreDeletedEvent(command.Id, deletedLore), ct);

            return Unit.Value;
        }
    }

    public class SuggestLoreCommandHandler : ICommandHandler<SuggestLoreCommand, LoreSuggestResponse>
    {
        private readonly ILoreRepository _repository;
        private readonly IAgentConversationService _agentService;

        public SuggestLoreCommandHandler(ILoreRepository repository, IAgentConversationService agentService)
        {
            _repository = repository;
            _agentService = agentService;
        }

        public async Task<LoreSuggestResponse> HandleAsync(SuggestLoreCommand command, CancellationToken ct = default)
        {
            var request = new LoreSuggestRequest(command.WorldId, command.Prompt, command.Count, command.DryRun);
            var items = await _agentService.GenerateLoreAsync(request, ct);

            if (items.Length == 0)
                return new LoreSuggestResponse(false, Array.Empty<LoreSuggestedItem>());

            if (command.DryRun) // no persist
                return new LoreSuggestResponse(false, items);

            // Persist generated lore
            var now = DateTimeOffset.UtcNow;
            foreach (var item in items)
            {
                var lore = new Lore
                {
                    Title = item.Title,
                    Text = item.Text,
                    WorldId = command.WorldId,
                    IsGenerated = true,
                    GenerationSource = "agent",
                    GenerationMeta = item.Model is null ? null : $"{{\"model\":\"{item.Model}\"}}",
                    GeneratedAt = now
                };
                await _repository.AddAsync(lore, ct);
            }

            return new LoreSuggestResponse(true, items);
        }
    }
}