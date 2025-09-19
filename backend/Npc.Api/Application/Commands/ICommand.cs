namespace Npc.Api.Application.Commands
{
    public interface ICommand<out TResult>
    {
    }

    public interface ICommand : ICommand<Unit>
    {
    }

    public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
    {
        Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
    }

    public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit> where TCommand : ICommand<Unit>
    {
    }

    // Unit type for commands that don't return a value
    public readonly struct Unit
    {
        public static readonly Unit Value = new();
    }
}