using System.Diagnostics;
using System.Text.Json;

namespace WinAPX.Core.Commands;

public sealed class LaunchAppCommand : ICommand
{
    private readonly string envName;
    private readonly string appId;

    public LaunchAppCommand(string envName, string appId)
    {
        this.envName = envName;
        this.appId = appId;
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            var metaPath = ApxPaths.AppMetaFile(envName, appId);
            if (!File.Exists(metaPath))
                return new CommandResult { Ok = false, Error = $"error: app '{appId}' not found in env '{envName}'" };

            var json = await File.ReadAllTextAsync(metaPath, cancellationToken);
            var meta = JsonSerializer.Deserialize<ExportedApp>(json);
            if (meta is null)
                return new CommandResult { Ok = false, Error = "error: app metadata is corrupt" };

            Process.Start(new ProcessStartInfo
            {
                FileName = meta.MasterShortcutPath,
                UseShellExecute = true
            });

            context.Emit($"Launched '{meta.DisplayName}'.");
            return new CommandResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new CommandResult { Ok = false, Error = ex.Message };
        }
    }
}
