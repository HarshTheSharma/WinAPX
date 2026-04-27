namespace WinAPX.Core.Commands;

public sealed class ExportEnvCommand : ICommand
{
    private readonly string envName;
    private readonly string outputPath;

    public ExportEnvCommand(string envName, string outputPath)
    {
        this.envName = ApxPaths.CleanName(envName);
        this.outputPath = outputPath;
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (envName.Length == 0)
            {
                return new CommandResult { Ok = false, Error = "error: missing env name" };
            }

            if (!await context.WslBackend.DistroExistsAsync(envName, cancellationToken))
            {
                return new CommandResult { Ok = false, Error = $"error: WSL distro '{envName}' not found" };
            }

            // Ensure output parent directory exists
            var outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            context.Emit($"Terminating '{envName}' before export...");
            await context.WslBackend.RunAsync(
                new[] { "--terminate", envName },
                _ => { },
                e => context.Emit("[err] " + e),
                cancellationToken);

            context.Emit($"Exporting '{envName}' to: {outputPath}");
            var exitCode = await context.WslBackend.RunAsync(
                new[] { "--export", envName, outputPath },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
            {
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: wsl --export failed" };
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
