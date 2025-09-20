using AutoMapper;
using Npc.Api.Entities;
using Npc.Api.Dtos;
using Npc.Api.Repositories;
using Npc.Api.Services;
using Npc.Api.Infrastructure.Audit;
using Npc.Api.Domain.Events;

namespace Npc.Api.Application.Commands
{
    // Bulk Command DTOs
    public record BulkCreateCharactersCommand(IEnumerable<CharacterRequest> Characters) : ICommand<BulkOperationResult<Character>>;
    public record BulkUpdateCharactersCommand(IEnumerable<(Guid Id, CharacterRequest Request)> Characters) : ICommand<BulkOperationResult<Character>>;
    public record BulkDeleteCharactersCommand(IEnumerable<Guid> CharacterIds) : ICommand<BulkOperationResult<Unit>>;

    // Bulk Result DTO
    public record BulkOperationResult<T>(
        IEnumerable<T> SuccessfulItems,
        IEnumerable<BulkOperationError> Errors,
        int TotalProcessed,
        int SuccessCount,
        int ErrorCount,
        TimeSpan ProcessingTime);

    public record BulkOperationError(
        string ItemIdentifier,
        string ErrorType,
        string ErrorMessage,
        Exception? Exception = null);

    // Bulk Command Handlers
    public class BulkCreateCharactersCommandHandler : ICommandHandler<BulkCreateCharactersCommand, BulkOperationResult<Character>>
    {
        private readonly ICharacterRepository _repository;
        private readonly IModerationService _moderationService;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly IMapper _mapper;
        private readonly ILogger<BulkCreateCharactersCommandHandler> _logger;

        public BulkCreateCharactersCommandHandler(
            ICharacterRepository repository,
            IModerationService moderationService,
            IAuditService auditService,
            IDomainEventDispatcher eventDispatcher,
            IMapper mapper,
            ILogger<BulkCreateCharactersCommandHandler> logger)
        {
            _repository = repository;
            _moderationService = moderationService;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<BulkOperationResult<Character>> HandleAsync(BulkCreateCharactersCommand command, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            var successfulItems = new List<Character>();
            var errors = new List<BulkOperationError>();
            var charactersArray = command.Characters.ToArray();

            _logger.LogInformation("Starting bulk creation of {Count} characters", charactersArray.Length);

            // Process in batches to avoid overwhelming the system
            const int batchSize = 50;
            var batches = charactersArray.Chunk(batchSize);

            foreach (var batch in batches)
            {
                var batchResults = await ProcessCreateBatch(batch, ct);
                successfulItems.AddRange(batchResults.SuccessfulItems);
                errors.AddRange(batchResults.Errors);
            }

            var processingTime = DateTime.UtcNow - startTime;

            _logger.LogInformation("Bulk character creation completed: {SuccessCount} successful, {ErrorCount} errors in {ProcessingTime}ms",
                successfulItems.Count, errors.Count, processingTime.TotalMilliseconds);

            return new BulkOperationResult<Character>(
                successfulItems,
                errors,
                charactersArray.Length,
                successfulItems.Count,
                errors.Count,
                processingTime);
        }

        private async Task<(IEnumerable<Character> SuccessfulItems, IEnumerable<BulkOperationError> Errors)> ProcessCreateBatch(
            CharacterRequest[] batch, CancellationToken ct)
        {
            var successfulItems = new List<Character>();
            var errors = new List<BulkOperationError>();

            // Validate all characters first
            var validCharacters = new List<(CharacterRequest Request, Character Entity)>();

            foreach (var request in batch)
            {
                try
                {
                    // Basic validation
                    if (string.IsNullOrWhiteSpace(request.Name))
                    {
                        errors.Add(new BulkOperationError(
                            request.Name ?? "Unknown",
                            "ValidationError",
                            "Character name is required"));
                        continue;
                    }

                    // Moderation check
                    var advisory = await _moderationService.AnalyzeAsync(request.Age, request.Description, ct);

                    var character = _mapper.Map<Character>(request);
                    validCharacters.Add((request, character));
                }
                catch (Exception ex)
                {
                    errors.Add(new BulkOperationError(
                        request.Name ?? "Unknown",
                        ex.GetType().Name,
                        ex.Message,
                        ex));
                }
            }

            // Bulk insert valid characters
            if (validCharacters.Any())
            {
                try
                {
                    var entities = validCharacters.Select(x => x.Entity).ToArray();
                    var createdCharacters = await _repository.BulkAddAsync(entities, ct);

                    successfulItems.AddRange(createdCharacters);

                    // Audit trail and events for successful items
                    var auditTasks = createdCharacters.Select(async character =>
                    {
                        await _auditService.LogCharacterChangeAsync("BULK_CREATE", character.Id, null, character, "api-user", ct);
                        await _eventDispatcher.DispatchAsync(new CharacterCreatedEvent(character), ct);
                    });

                    await Task.WhenAll(auditTasks);
                }
                catch (Exception ex)
                {
                    // If bulk insert fails, add errors for all items in this batch
                    foreach (var (request, _) in validCharacters)
                    {
                        errors.Add(new BulkOperationError(
                            request.Name,
                            "BulkInsertError",
                            $"Bulk insert failed: {ex.Message}",
                            ex));
                    }
                }
            }

            return (successfulItems, errors);
        }
    }

    public class BulkUpdateCharactersCommandHandler : ICommandHandler<BulkUpdateCharactersCommand, BulkOperationResult<Character>>
    {
        private readonly ICharacterRepository _repository;
        private readonly IModerationService _moderationService;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly IMapper _mapper;
        private readonly ILogger<BulkUpdateCharactersCommandHandler> _logger;

        public BulkUpdateCharactersCommandHandler(
            ICharacterRepository repository,
            IModerationService moderationService,
            IAuditService auditService,
            IDomainEventDispatcher eventDispatcher,
            IMapper mapper,
            ILogger<BulkUpdateCharactersCommandHandler> logger)
        {
            _repository = repository;
            _moderationService = moderationService;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<BulkOperationResult<Character>> HandleAsync(BulkUpdateCharactersCommand command, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            var successfulItems = new List<Character>();
            var errors = new List<BulkOperationError>();
            var charactersArray = command.Characters.ToArray();

            _logger.LogInformation("Starting bulk update of {Count} characters", charactersArray.Length);

            // Process each character individually for updates (safer than bulk updates)
            foreach (var (id, request) in charactersArray)
            {
                try
                {
                    var existingCharacter = await _repository.GetByIdAsync(id, ct);
                    if (existingCharacter == null)
                    {
                        errors.Add(new BulkOperationError(
                            id.ToString(),
                            "EntityNotFound",
                            $"Character with ID {id} not found"));
                        continue;
                    }

                    // Moderation check
                    var advisory = await _moderationService.AnalyzeAsync(request.Age, request.Description, ct);

                    // Capture old values for audit
                    var oldCharacter = new { existingCharacter.Name, existingCharacter.Age, existingCharacter.Description, existingCharacter.AvatarUrl };

                    // Update properties
                    _mapper.Map(request, existingCharacter);

                    var updatedCharacter = await _repository.UpdateAsync(existingCharacter, ct);
                    successfulItems.Add(updatedCharacter);

                    // Audit trail and events
                    await _auditService.LogCharacterChangeAsync("BULK_UPDATE", updatedCharacter.Id, oldCharacter, updatedCharacter, "api-user", ct);
                    await _eventDispatcher.DispatchAsync(new CharacterUpdatedEvent(updatedCharacter, oldCharacter), ct);
                }
                catch (Exception ex)
                {
                    errors.Add(new BulkOperationError(
                        id.ToString(),
                        ex.GetType().Name,
                        ex.Message,
                        ex));
                }
            }

            var processingTime = DateTime.UtcNow - startTime;

            _logger.LogInformation("Bulk character update completed: {SuccessCount} successful, {ErrorCount} errors in {ProcessingTime}ms",
                successfulItems.Count, errors.Count, processingTime.TotalMilliseconds);

            return new BulkOperationResult<Character>(
                successfulItems,
                errors,
                charactersArray.Length,
                successfulItems.Count,
                errors.Count,
                processingTime);
        }
    }

    public class BulkDeleteCharactersCommandHandler : ICommandHandler<BulkDeleteCharactersCommand, BulkOperationResult<Unit>>
    {
        private readonly ICharacterRepository _repository;
        private readonly IAuditService _auditService;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly ILogger<BulkDeleteCharactersCommandHandler> _logger;

        public BulkDeleteCharactersCommandHandler(
            ICharacterRepository repository,
            IAuditService auditService,
            IDomainEventDispatcher eventDispatcher,
            ILogger<BulkDeleteCharactersCommandHandler> logger)
        {
            _repository = repository;
            _auditService = auditService;
            _eventDispatcher = eventDispatcher;
            _logger = logger;
        }

        public async Task<BulkOperationResult<Unit>> HandleAsync(BulkDeleteCharactersCommand command, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            var successfulItems = new List<Unit>();
            var errors = new List<BulkOperationError>();
            var idsArray = command.CharacterIds.ToArray();

            _logger.LogInformation("Starting bulk deletion of {Count} characters", idsArray.Length);

            // Get all characters first to capture audit data
            var charactersToDelete = new List<(Guid Id, object AuditData)>();

            foreach (var id in idsArray)
            {
                try
                {
                    var character = await _repository.GetByIdAsync(id, ct);
                    if (character == null)
                    {
                        errors.Add(new BulkOperationError(
                            id.ToString(),
                            "EntityNotFound",
                            $"Character with ID {id} not found"));
                        continue;
                    }

                    charactersToDelete.Add((id, new { character.Id, character.Name, character.Age, character.Description, character.AvatarUrl }));
                }
                catch (Exception ex)
                {
                    errors.Add(new BulkOperationError(
                        id.ToString(),
                        ex.GetType().Name,
                        ex.Message,
                        ex));
                }
            }

            // Bulk delete
            if (charactersToDelete.Any())
            {
                try
                {
                    var idsToDelete = charactersToDelete.Select(x => x.Id).ToArray();
                    await _repository.BulkDeleteAsync(idsToDelete, ct);

                    // Audit trail and events for successful deletions
                    var auditTasks = charactersToDelete.Select(async item =>
                    {
                        await _auditService.LogCharacterChangeAsync("BULK_DELETE", item.Id, item.AuditData, null, "api-user", ct);
                        await _eventDispatcher.DispatchAsync(new CharacterDeletedEvent(item.Id, item.AuditData), ct);
                        successfulItems.Add(Unit.Value);
                    });

                    await Task.WhenAll(auditTasks);
                }
                catch (Exception ex)
                {
                    // If bulk delete fails, add errors for all items
                    foreach (var (id, _) in charactersToDelete)
                    {
                        errors.Add(new BulkOperationError(
                            id.ToString(),
                            "BulkDeleteError",
                            $"Bulk delete failed: {ex.Message}",
                            ex));
                    }
                }
            }

            var processingTime = DateTime.UtcNow - startTime;

            _logger.LogInformation("Bulk character deletion completed: {SuccessCount} successful, {ErrorCount} errors in {ProcessingTime}ms",
                successfulItems.Count, errors.Count, processingTime.TotalMilliseconds);

            return new BulkOperationResult<Unit>(
                successfulItems,
                errors,
                idsArray.Length,
                successfulItems.Count,
                errors.Count,
                processingTime);
        }
    }
}