namespace Npc.Api.Domain.Events
{
    public interface IDomainEvent
    {
        Guid Id { get; }
        DateTimeOffset OccurredAt { get; }
        string EventType { get; }
    }

    public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
    {
        Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
    }

    public interface IDomainEventDispatcher
    {
        Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default);
        Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default);
    }

    public abstract record DomainEvent : IDomainEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
        public virtual string EventType => GetType().Name;
    }
}