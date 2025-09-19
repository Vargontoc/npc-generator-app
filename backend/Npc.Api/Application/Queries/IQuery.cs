namespace Npc.Api.Application.Queries
{
    public interface IQuery<out TResult>
    {
    }

    public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
    {
        Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
    }
}