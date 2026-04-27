using System.Text.Json;

namespace WinAPX.Core.Commands;

public sealed class ListAppsCommand : ICommand
{
    private readonly string? envFilter;

    public ListAppsCommand(string? envFilter = null)
    {
        this.envFilter = string.IsNullOrWhiteSpace(envFilter) ? null : envFilter;
    }

    public Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(ApxPaths.Apps))
            {
                context.Emit("No exported apps.");
                return Task.FromResult(new CommandResult { Ok = true });
            }

            var found = false;
            foreach (var app in EnumerateAll(envFilter))
            {
                context.Emit($"{app.EnvName} / {app.Id}   {app.DisplayName}   {app.Command}");
                found = true;
            }

            if (!found)
            {
                context.Emit("No exported apps.");
            }

            return Task.FromResult(new CommandResult { Ok = true });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CommandResult { Ok = false, Error = ex.Message });
        }
    }

    public static IEnumerable<ExportedApp> EnumerateAll(string? envFilter = null)
    {
        if (!Directory.Exists(ApxPaths.Apps))
        {
            yield break;
        }

        IEnumerable<string> envDirs;
        if (envFilter is null)
        {
            envDirs = Directory.EnumerateDirectories(ApxPaths.Apps);
        }
        else
        {
            var single = ApxPaths.AppsForEnv(envFilter);
            envDirs = Directory.Exists(single) ? new[] { single } : Array.Empty<string>();
        }

        foreach (var envDir in envDirs)
        {
            foreach (var appDir in Directory.EnumerateDirectories(envDir))
            {
                var metaPath = Path.Combine(appDir, "app.json");
                if (!File.Exists(metaPath))
                {
                    continue;
                }
                ExportedApp? meta = null;
                try
                {
                    meta = JsonSerializer.Deserialize<ExportedApp>(File.ReadAllText(metaPath));
                }
                catch { /* skip corrupt entries */ }
                if (meta is not null)
                {
                    yield return meta;
                }
            }
        }
    }
}
