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
    string Name { get; }
    Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken);
}

public interface ICommandContext
{
    WslBackend WslBackend { get; }
    void Emit(string message);
}
