namespace WinAPX.Core.Commands;

public sealed class ImportEnvCommand : ICommand
{
    private readonly string envName;
    private readonly string tarPath;
    private readonly string? installLocation;

    public ImportEnvCommand(string envName, string tarPath, string? installLocation = null)
    {
        this.envName = ApxPaths.CleanName(envName);
        this.tarPath = tarPath;
        this.installLocation = string.IsNullOrWhiteSpace(installLocation) ? null : installLocation;
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (envName.Length == 0)
            {
                return new CommandResult { Ok = false, Error = "error: missing env name" };
            }

            if (CreateCommand.ReservedNames.Contains(envName))
            {
                return new CommandResult { Ok = false, Error = $"error: '{envName}' is a reserved command name; pick another" };
            }

            if (!File.Exists(tarPath))
            {
                return new CommandResult { Ok = false, Error = $"error: tar file not found: {tarPath}" };
            }

            ApxPaths.EnsureBaseDirs();

            if (await context.WslBackend.DistroExistsAsync(envName, cancellationToken))
            {
                return new CommandResult { Ok = false, Error = $"error: WSL distro '{envName}' already exists" };
            }

            var instanceDir = installLocation is null
                ? ApxPaths.InstanceDir(envName)
                : Path.Combine(installLocation, envName);

            Directory.CreateDirectory(instanceDir);

            if (Directory.EnumerateFileSystemEntries(instanceDir).Any())
            {
                return new CommandResult { Ok = false, Error = $"error: instance dir not empty: {instanceDir}" };
            }

            context.Emit($"Importing '{envName}' from: {tarPath}");
            var exitCode = await context.WslBackend.RunAsync(
                new[] { "--import", envName, instanceDir, tarPath },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
            {
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: wsl --import failed" };
            }

            context.Emit("Done.");
            return new CommandResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new CommandResult { Ok = false, Error = ex.Message };
        }
    }
}
