using System.Reflection;

namespace Npc.Api.Domain.Events
{
    public class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DomainEventDispatcher> _logger;

        public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
        {
            await DispatchAsync(new[] { domainEvent }, ct);
        }

        public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
        {
            foreach (var domainEvent in domainEvents)
            {
                try
                {
                    await DispatchSingleEventAsync(domainEvent, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error dispatching domain event {EventType} with ID {EventId}",
                        domainEvent.EventType, domainEvent.Id);
                    // Don't rethrow - we want other events to continue processing
                }
            }
        }

        private async Task DispatchSingleEventAsync(IDomainEvent domainEvent, CancellationToken ct)
        {
            var eventType = domainEvent.GetType();
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

            using var scope = _serviceProvider.CreateScope();
            var handlers = scope.ServiceProvider.GetServices(handlerType);

            if (!handlers.Any())
            {
                _logger.LogDebug("No handlers found for domain event {EventType}", domainEvent.EventType);
                return;
            }

            var handleMethod = handlerType.GetMethod("HandleAsync");
            if (handleMethod == null)
            {
                _logger.LogWarning("HandleAsync method not found for {HandlerType}", handlerType);
                return;
            }

            var tasks = handlers.Select(handler =>
            {
                try
                {
                    var task = (Task)handleMethod.Invoke(handler, [domainEvent, ct])!;
                    return task;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invoking handler {HandlerType} for event {EventType}",
                        handler == null ? "Unknown handler" : handler.GetType().Name, domainEvent.EventType);
                    return Task.CompletedTask;
                }
            });

            await Task.WhenAll(tasks);
        }
    }
}