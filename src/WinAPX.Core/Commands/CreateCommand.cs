namespace WinAPX.Core.Commands;

public sealed class CreateCommand : ICommand
{
    public string Name => "create";
    private readonly string envName;

    public CreateCommand(string envName)
    {
        this.envName = PathUtils.CleanName(envName);
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (envName.Length == 0)
                return new CommandResult { Ok = false, Error = "error: missing env name" };

            ApxPaths.EnsureBaseDirs();

            if (await context.WslBackend.DistroExistsAsync(envName, cancellationToken))
                return new CommandResult { Ok = false, Error = $"error: WSL distro '{envName}' already exists" };

            context.Emit("Ensuring base Ubuntu tar exists...");
            await context.WslBackend.EnsureUbuntuBaseTarAsync(
                ApxPaths.UbuntuBaseTarPath,
                msg => context.Emit(msg),
                cancellationToken);

            var instanceDir = ApxPaths.InstanceDir(envName);
            Directory.CreateDirectory(instanceDir);

            if (Directory.EnumerateFileSystemEntries(instanceDir).Any())
                return new CommandResult { Ok = false, Error = $"error: instance dir not empty: {instanceDir}" };

            context.Emit($"Importing '{envName}' from base Ubuntu...");
            var exitCode = await context.WslBackend.RunAsync(
                new[] { "--import", envName, instanceDir, ApxPaths.UbuntuBaseTarPath },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: wsl --import failed" };

            context.Emit("Done.");
            return new CommandResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new CommandResult { Ok = false, Error = ex.Message };
        }
    }
}
