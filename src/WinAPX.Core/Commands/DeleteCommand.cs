using Microsoft.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinAPX.Core.Commands;

public sealed class DeleteCommand : ICommand
{
    public string Name => "delete";
    private readonly string envName;
    private readonly bool keepFiles;

    public DeleteCommand(string envName, bool keepFiles = false)
    {
        this.envName = PathUtils.CleanName(envName);
        this.keepFiles = keepFiles;
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (envName.Length == 0)
                return new CommandResult { Ok = false, Error = "error: missing env name" };

            if (!await context.WslBackend.DistroExistsAsync(envName, cancellationToken))
                return new CommandResult { Ok = false, Error = $"error: WSL distro '{envName}' not found" };

            // Capture where WSL stores ext4.vhdx (works even if create used a user-picked folder).
            var basePath = TryGetWslBasePath(envName);

            context.Emit($"Terminating '{envName}'...");
            await context.WslBackend.RunAsync(
                new[] { "--terminate", envName },
                _ => { },
                _ => { },
                cancellationToken);

            context.Emit($"Unregistering '{envName}'...");
            var exitCode = await context.WslBackend.RunAsync(
                new[] { "--unregister", envName },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: wsl --unregister failed" };

            if (keepFiles)
            {
                context.Emit("Done. (files kept)");
                return new CommandResult { Ok = true };
            }

            // Prefer WSL's BasePath; fallback to WinAPX default instance dir.
            var deletePath = !string.IsNullOrWhiteSpace(basePath)
                ? basePath
                : ApxPaths.InstanceDir(envName);

            if (Directory.Exists(deletePath))
            {
                context.Emit($"Deleting files: {deletePath}");
                Directory.Delete(deletePath, recursive: true);
            }
            else
            {
                context.Emit($"Files already missing: {deletePath}");
            }

            context.Emit("Done.");
            return new CommandResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new CommandResult { Ok = false, Error = ex.Message };
        }
    }

    private static string? TryGetWslBasePath(string distroName)
    {
        // HKCU\Software\Microsoft\Windows\CurrentVersion\Lxss\<GUID>
        using var lxss = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
        if (lxss is null) return null;

        foreach (var subName in lxss.GetSubKeyNames())
        {
            using var sub = lxss.OpenSubKey(subName);
            if (sub is null) continue;

            var name = sub.GetValue("DistributionName") as string;
            if (!string.Equals(name, distroName, StringComparison.OrdinalIgnoreCase))
                continue;

            return sub.GetValue("BasePath") as string;
        }

        return null;
    }
}