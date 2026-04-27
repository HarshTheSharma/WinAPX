namespace WinAPX.Core;

public sealed record CommandEvent(DateTimeOffset At, string Message);

public sealed class CommandResult
{
    public bool Ok { get; init; }
    public int? ExitCode { get; init; }
    public string? Error { get; init; }
}

public interface ICommand
{
    Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken);
}

public interface ICommandContext
{
    WslBackend WslBackend { get; }
    void Emit(string message);
}

public sealed class CommandDispatcher
{
    private readonly WslBackend wslBackend = new();

    public Task<List<string>> ListDistrosAsync(CancellationToken cancellationToken)
        => wslBackend.ListDistrosQuietAsync(cancellationToken);

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
