using System;
using System.Threading;
using System.Threading.Tasks;

namespace WinAPX.Core.Commands;

public sealed class EnterCommand : ICommand
{
    public string Name => "enter";
    private readonly string envName;

    public EnterCommand(string envName)
    {
        this.envName = PathUtils.CleanName(envName);
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (envName.Length == 0)
                return new CommandResult { Ok = false, Error = "error: missing env name" };

            if (!await context.WslBackend.DistroExistsAsync(envName, cancellationToken))
                return new CommandResult { Ok = false, Error = $"error: WSL distro '{envName}' not found" };

            context.Emit($"Entering '{envName}'...");
            TerminalLauncher.LaunchInteractive(
                context.WslBackend.ExePath,
                new[] { "-d", envName });

            context.Emit("Done.");
            return new CommandResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new CommandResult { Ok = false, Error = ex.Message };
        }
    }
}