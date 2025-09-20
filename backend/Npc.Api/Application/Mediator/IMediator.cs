using Npc.Api.Application.Commands;
using Npc.Api.Application.Queries;

namespace Npc.Api.Application.Mediator
{
    public interface IMediator
    {
        Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
        Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
    }

    public class SimpleMediator : IMediator
    {
        private readonly IServiceProvider _serviceProvider;

        public SimpleMediator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default)
        {
            var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
            var handler = _serviceProvider.GetRequiredService(handlerType);

            var method = handlerType.GetMethod("HandleAsync");
            if (method == null)
                throw new InvalidOperationException($"HandleAsync method not found for {handlerType}");

            var task = (Task<TResult>)method.Invoke(handler, new object[] { command, ct })!;
            return await task;
        }

        public async Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
        {
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
            var handler = _serviceProvider.GetRequiredService(handlerType);

            var method = handlerType.GetMethod("HandleAsync");
            if (method == null)
                throw new InvalidOperationException($"HandleAsync method not found for {handlerType}");

            var task = (Task<TResult>)method.Invoke(handler, [query, ct])!;
            return await task;
        }
    }
}