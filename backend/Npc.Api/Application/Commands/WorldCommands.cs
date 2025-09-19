using Npc.Api.Entities;
using Npc.Api.Repositories;
using Npc.Api.Infrastructure.Audit;

namespace Npc.Api.Application.Commands
{
    // Command DTOs
    public record CreateWorldCommand(string Name, string? Description) : ICommand<World>;
    public record UpdateWorldCommand(Guid Id, string Name, string? Description) : ICommand<World>;
    public record DeleteWorldCommand(Guid Id) : ICommand;

    // Command Handlers
    public class CreateWorldCommandHandler : ICommandHandler<CreateWorldCommand, World>
    {
        private readonly IWorldRepository _repository;
        private readonly IAuditService _auditService;

        public CreateWorldCommandHandler(IWorldRepository repository, IAuditService auditService)
        {
            _repository = repository;
            _auditService = auditService;
        }

        public async Task<World> HandleAsync(CreateWorldCommand command, CancellationToken ct = default)
        {
            var world = new World
            {
                Name = command.Name,
                Description = command.Description
            };

            var createdWorld = await _repository.AddAsync(world, ct);

            // Audit trail
            await _auditService.LogWorldChangeAsync("CREATE", createdWorld.Id, null, createdWorld, "api-user", ct);

            return createdWorld;
        }
    }

    public class UpdateWorldCommandHandler : ICommandHandler<UpdateWorldCommand, World>
    {
        private readonly IWorldRepository _repository;
        private readonly IAuditService _auditService;

        public UpdateWorldCommandHandler(IWorldRepository repository, IAuditService auditService)
        {
            _repository = repository;
            _auditService = auditService;
        }

        public async Task<World> HandleAsync(UpdateWorldCommand command, CancellationToken ct = default)
        {
            var existingWorld = await _repository.GetByIdAsync(command.Id, ct);
            if (existingWorld == null)
                throw new InvalidOperationException($"World with ID {command.Id} not found");

            // Capture old values for audit
            var oldWorld = new { existingWorld.Name, existingWorld.Description };

            // Update properties
            existingWorld.Name = command.Name;
            existingWorld.Description = command.Description;

            var updatedWorld = await _repository.UpdateAsync(existingWorld, ct);

            // Audit trail
            await _auditService.LogWorldChangeAsync("UPDATE", updatedWorld.Id, oldWorld, updatedWorld, "api-user", ct);

            return updatedWorld;
        }
    }

    public class DeleteWorldCommandHandler : ICommandHandler<DeleteWorldCommand>
    {
        private readonly IWorldRepository _repository;
        private readonly IAuditService _auditService;

        public DeleteWorldCommandHandler(IWorldRepository repository, IAuditService auditService)
        {
            _repository = repository;
            _auditService = auditService;
        }

        public async Task<Unit> HandleAsync(DeleteWorldCommand command, CancellationToken ct = default)
        {
            var world = await _repository.GetByIdAsync(command.Id, ct);
            if (world == null)
                throw new InvalidOperationException($"World with ID {command.Id} not found");

            // Capture entity for audit before deletion
            var deletedWorld = new { world.Id, world.Name, world.Description };

            await _repository.DeleteAsync(command.Id, ct);

            // Audit trail
            await _auditService.LogWorldChangeAsync("DELETE", command.Id, deletedWorld, null, "api-user", ct);

            return Unit.Value;
        }
    }
}