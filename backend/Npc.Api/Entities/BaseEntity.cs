namespace Npc.Api.Entities
{
    public abstract class BaseEntity : IEntity
    {
        public virtual Guid Id { get; set; } = Guid.NewGuid();
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}