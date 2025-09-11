namespace Npc.Api.Entities
{
    public abstract class BaseEntity
    {
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdateAt { get; set; }
    }
}