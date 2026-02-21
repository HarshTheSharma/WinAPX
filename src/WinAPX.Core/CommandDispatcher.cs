namespace WinAPX.Core;

public sealed class CommandDispatcher
{
    private readonly WslBackend wslBackend = new();

    public Task<CommandResult> RunAsync(
        ICommand command,
        Action<CommandEvent> onEvent,
        CancellationToken cancellationToken)
    {
        var context = new CommandContext(wslBackend, onEvent);
        return command.ExecuteAsync(context, cancellationToken);
    }

    private sealed class CommandContext : ICommandContext
    {
        private readonly Action<CommandEvent> onEvent;

        public CommandContext(WslBackend wslBackend, Action<CommandEvent> onEvent)
        {
            WslBackend = wslBackend;
            this.onEvent = onEvent;
        }

        public WslBackend WslBackend { get; }

        public void Emit(string message)
            => onEvent(new CommandEvent(DateTimeOffset.Now, message));
    }
}
