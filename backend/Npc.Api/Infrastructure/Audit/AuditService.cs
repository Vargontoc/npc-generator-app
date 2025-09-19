using System.Text.Json;

namespace Npc.Api.Infrastructure.Audit
{
    public interface IAuditService
    {
        Task LogAsync(string action, string entityType, string entityId, object? oldValue, object? newValue, string? userId = null, CancellationToken ct = default);
        Task LogCharacterChangeAsync(string action, Guid characterId, object? oldValue, object? newValue, string? userId = null, CancellationToken ct = default);
        Task LogWorldChangeAsync(string action, Guid worldId, object? oldValue, object? newValue, string? userId = null, CancellationToken ct = default);
        Task LogLoreChangeAsync(string action, Guid loreId, object? oldValue, object? newValue, string? userId = null, CancellationToken ct = default);
    }

    public class AuditService(ILogger<AuditService> logger) : IAuditService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        public async Task LogAsync(string action, string entityType, string entityId, object? oldValue, object? newValue, string? userId = null, CancellationToken ct = default)
        {
            var auditEntry = new
            {
                Timestamp = DateTimeOffset.UtcNow,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                UserId = userId ?? "system",
                OldValue = oldValue != null ? JsonSerializer.Serialize(oldValue, JsonOptions) : null,
                NewValue = newValue != null ? JsonSerializer.Serialize(newValue, JsonOptions) : null,
                Environment = Environment.MachineName,
                ProcessId = Environment.ProcessId
            };

            // Log as structured data for better parsing
            logger.LogInformation("AUDIT: {Action} on {EntityType} {EntityId} by {UserId}. Old: {OldValue}, New: {NewValue}",
                action, entityType, entityId, userId ?? "system",
                auditEntry.OldValue, auditEntry.NewValue);

            // For production, you might want to store this in a separate audit database
            await Task.CompletedTask;
        }

        public Task LogCharacterChangeAsync(string action, Guid characterId, object? oldValue, object? newValue, string? userId = null, CancellationToken ct = default)
            => LogAsync(action, "Character", characterId.ToString(), oldValue, newValue, userId, ct);

        public Task LogWorldChangeAsync(string action, Guid worldId, object? oldValue, object? newValue, string? userId = null, CancellationToken ct = default)
            => LogAsync(action, "World", worldId.ToString(), oldValue, newValue, userId, ct);

        public Task LogLoreChangeAsync(string action, Guid loreId, object? oldValue, object? newValue, string? userId = null, CancellationToken ct = default)
            => LogAsync(action, "Lore", loreId.ToString(), oldValue, newValue, userId, ct);
    }
}