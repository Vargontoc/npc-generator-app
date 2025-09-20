using AutoMapper;
using Npc.Api.Entities;
using Npc.Api.Dtos;
using Npc.Api.Repositories;
using Npc.Api.Infrastructure.Audit;
using Npc.Api.Infrastructure.Exceptions;
using Npc.Api.Domain.Events;

namespace Npc.Api.Application.Commands
{
    // Command DTOs
    public record CreateWorldCommand(WorldRequest Request) : ICommand<World>;
    public record UpdateWorldCommand(Guid Id, WorldRequest Request) : ICommand<World>;
    public record DeleteWorldCommand(Guid Id) : ICommand;

    // Command Handlers
    public class CreateWorldCommandHandler : ICommandHandler<CreateWorldCommand, World>
    {
        private readonly IWorldRepository _repository;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly IMapper _mapper;

        public CreateWorldCommandHandler(IWorldRepository repository, IAuditService auditService, IDomainEventDispatcher eventDispatcher, IMapper mapper)
        {
            _repository = repository;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
            _mapper = mapper;
        }

        public async Task<World> HandleAsync(CreateWorldCommand command, CancellationToken ct = default)
        {
            // Use AutoMapper to convert DTO to Entity
            var world = _mapper.Map<World>(command.Request);

            var createdWorld = await _repository.AddAsync(world, ct);

            // Audit trail
            await _auditService.LogWorldChangeAsync("CREATE", createdWorld.Id, null, createdWorld, "api-user", ct);

            // Dispatch domain event for database synchronization
            await _eventDispatcher.DispatchAsync(new WorldCreatedEvent(createdWorld), ct);

            return createdWorld;
        }
    }

    public class UpdateWorldCommandHandler : ICommandHandler<UpdateWorldCommand, World>
    {
        private readonly IWorldRepository _repository;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly IMapper _mapper;

        public UpdateWorldCommandHandler(IWorldRepository repository, IAuditService auditService, IDomainEventDispatcher eventDispatcher, IMapper mapper)
        {
            _repository = repository;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
            _mapper = mapper;
        }

        public async Task<World> HandleAsync(UpdateWorldCommand command, CancellationToken ct = default)
        {
            var existingWorld = await _repository.GetByIdAsync(command.Id, ct);
            if (existingWorld == null)
                throw new EntityNotFoundException("World", command.Id);

            // Capture old values for audit
            var oldWorld = new { existingWorld.Name, existingWorld.Description };

            // Use AutoMapper to update entity from DTO
            _mapper.Map(command.Request, existingWorld);

            var updatedWorld = await _repository.UpdateAsync(existingWorld, ct);

            // Audit trail
            await _auditService.LogWorldChangeAsync("UPDATE", updatedWorld.Id, oldWorld, updatedWorld, "api-user", ct);

            // Dispatch domain event for database synchronization
            await _eventDispatcher.DispatchAsync(new WorldUpdatedEvent(updatedWorld, oldWorld), ct);

            return updatedWorld;
        }
    }

    public class DeleteWorldCommandHandler : ICommandHandler<DeleteWorldCommand>
    {
        private readonly IWorldRepository _repository;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;

        public DeleteWorldCommandHandler(IWorldRepository repository, IAuditService auditService, IDomainEventDispatcher eventDispatcher)
        {
            _repository = repository;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
        }

        public async Task<Unit> HandleAsync(DeleteWorldCommand command, CancellationToken ct = default)
        {
            var world = await _repository.GetByIdAsync(command.Id, ct);
            if (world == null)
                throw new EntityNotFoundException("World", command.Id);

            // Capture entity for audit before deletion
            var deletedWorld = new { world.Id, world.Name, world.Description };

            await _repository.DeleteAsync(command.Id, ct);

            // Audit trail
            await _auditService.LogWorldChangeAsync("DELETE", command.Id, deletedWorld, null, "api-user", ct);

            // Dispatch domain event for database synchronization
            await _eventDispatcher.DispatchAsync(new WorldDeletedEvent(command.Id, deletedWorld), ct);

            return Unit.Value;
        }
    }
}