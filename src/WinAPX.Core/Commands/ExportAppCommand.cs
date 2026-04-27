using System.Diagnostics;
using System.Text.Json;

namespace WinAPX.Core.Commands;

public sealed class ExportAppCommand : ICommand
{
    private readonly string envName;
    private readonly string displayName;
    private readonly string command;
    private readonly string? iconPath;
    private readonly string? outputDir;
    private readonly bool launchAfter;

    public ExportAppCommand(
        string envName,
        string displayName,
        string command,
        string? iconPath = null,
        string? outputDir = null,
        bool launchAfter = true)
    {
        this.envName = ApxPaths.CleanName(envName);
        this.displayName = displayName;
        this.command = command;
        this.iconPath = iconPath;
        this.outputDir = outputDir;
        this.launchAfter = launchAfter;
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            var appId = Slugify(displayName);

            if (envName.Length == 0)
            {
                return new CommandResult { Ok = false, Error = "error: missing env name" };
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return new CommandResult { Ok = false, Error = "error: missing display name" };
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                return new CommandResult { Ok = false, Error = "error: missing command" };
            }

            if (!await context.WslBackend.DistroExistsAsync(envName, cancellationToken))
            {
                return new CommandResult { Ok = false, Error = $"error: WSL distro '{envName}' not found" };
            }

            if (Directory.Exists(ApxPaths.AppDir(envName, appId)))
            {
                return new CommandResult { Ok = false, Error = $"error: app '{appId}' already exported from this env" };
            }

            Directory.CreateDirectory(ApxPaths.AppDir(envName, appId));

            // Resolve icon
            var resolvedIconPath = ApxPaths.AppIconFile(envName, appId);
            if (iconPath is not null && File.Exists(iconPath))
            {
                File.Copy(iconPath, resolvedIconPath, overwrite: true);
            }
            else
            {
                var iconStream = typeof(ExportAppCommand).Assembly
                    .GetManifestResourceStream("WinAPX.Core.shortcut.ico");
                if (iconStream is not null)
                {
                    using (iconStream)
                    {
                        using var fs = File.Create(resolvedIconPath);
                        await iconStream.CopyToAsync(fs, cancellationToken);
                    }
                }
            }

            // Build master shortcut
            var escapedCmd = command.Replace("\"", "\\\"");
            var args = $"-d {envName} -- bash -lc \"{escapedCmd}\"";
            CreateShortcut(
                ApxPaths.AppMasterShortcut(envName, appId),
                context.WslBackend.ExePath,
                args,
                resolvedIconPath,
                workingDir: null,
                description: $"{displayName} ({envName})");

            // Desktop copy
            var targetDir = outputDir ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            Directory.CreateDirectory(targetDir);
            var desktopLnk = Path.Combine(targetDir, $"{displayName}.lnk");
            File.Copy(ApxPaths.AppMasterShortcut(envName, appId), desktopLnk, overwrite: true);

            // Write app.json
            var meta = new ExportedApp
            {
                Id = appId,
                DisplayName = displayName,
                EnvName = envName,
                Command = command,
                MasterShortcutPath = ApxPaths.AppMasterShortcut(envName, appId),
                IconPath = resolvedIconPath,
                ExportedCopies = new List<string> { desktopLnk },
                CreatedAt = DateTimeOffset.Now
            };
            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(ApxPaths.AppMetaFile(envName, appId), json, cancellationToken);

            context.Emit($"Exported '{displayName}' → {desktopLnk}");

            if (launchAfter)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = meta.MasterShortcutPath,
                        UseShellExecute = true
                    });
                    context.Emit($"Launched '{displayName}'.");
                }
                catch (Exception ex)
                {
                    context.Emit($"[warn] launch failed: {ex.Message}");
                }
            }

            return new CommandResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new CommandResult { Ok = false, Error = ex.Message };
        }
    }

    private static string Slugify(string input)
    {
        var lower = input.ToLowerInvariant();
        var slug = System.Text.RegularExpressions.Regex.Replace(lower, @"[^a-z0-9]+", "-");
        return slug.Trim('-');
    }

    private static void CreateShortcut(
        string lnkPath,
        string targetExe,
        string arguments,
        string? iconPath,
        string? workingDir,
        string? description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lnkPath)!);
        var type = Type.GetTypeFromProgID("WScript.Shell")
                   ?? throw new InvalidOperationException("WScript.Shell COM not available");
        dynamic shell = Activator.CreateInstance(type)!;
        dynamic link = shell.CreateShortcut(lnkPath);
        link.TargetPath = targetExe;
        link.Arguments = arguments;
        if (!string.IsNullOrEmpty(workingDir))
        {
            link.WorkingDirectory = workingDir;
        }
        if (!string.IsNullOrEmpty(iconPath))
        {
            link.IconLocation = iconPath;
        }
        if (!string.IsNullOrEmpty(description))
        {
            link.Description = description;
        }
        link.Save();
    }
}
