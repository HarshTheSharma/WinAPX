using System.Text.Json;

namespace WinAPX.Core.Commands;

public sealed class DeleteAppCommand : ICommand
{
    private readonly string envName;
    private readonly string appId;

    public DeleteAppCommand(string envName, string appId)
    {
        this.envName = envName;
        this.appId = appId;
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(envName) || string.IsNullOrWhiteSpace(appId))
                return new CommandResult { Ok = false, Error = "error: missing env name or app id" };

            var metaPath = ApxPaths.AppMetaFile(envName, appId);
            if (File.Exists(metaPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(metaPath, cancellationToken);
                    var meta = JsonSerializer.Deserialize<ExportedApp>(json);
                    if (meta is not null)
                    {
                        foreach (var copy in meta.ExportedCopies)
                        {
                            try
                            {
                                File.Delete(copy);
                            }
                            catch { /* best-effort */ }
                        }
                    }
                }
                catch
                {
                    context.Emit($"[warn] could not read app metadata for '{appId}'; proceeding with directory removal.");
                }
            }
            else
            {
                context.Emit($"[warn] app metadata not found for '{appId}'; removing directory if it exists.");
            }

            var appDir = ApxPaths.AppDir(envName, appId);
            if (Directory.Exists(appDir))
                Directory.Delete(appDir, recursive: true);

            return new CommandResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new CommandResult { Ok = false, Error = ex.Message };
        }
    }

    public static async Task DeleteAllForEnvAsync(string envName, ICommandContext ctx, CancellationToken ct)
    {
        var appsForEnv = ApxPaths.AppsForEnv(envName);
        if (!Directory.Exists(appsForEnv)) return;

        foreach (var appDir in Directory.EnumerateDirectories(appsForEnv))
        {
            var appId = Path.GetFileName(appDir);
            var cmd = new DeleteAppCommand(envName, appId);
            await cmd.ExecuteAsync(ctx, ct);
        }

        try { Directory.Delete(appsForEnv, recursive: true); }
        catch { /* best-effort */ }
    }
}
