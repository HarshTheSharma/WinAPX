using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinAPX.Core.Commands;

public sealed class EnterCommand : ICommand
{
    public string Name => "enter";
    private readonly string envName;
    private readonly string? startWindowsPath;

    private const string DefaultLinuxWkdir = "/wkdir";

    public EnterCommand(string envName, string? startWindowsPath = null)
    {
        this.envName = PathUtils.CleanName(envName);
        this.startWindowsPath = string.IsNullOrWhiteSpace(startWindowsPath) ? null : startWindowsPath;
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (envName.Length == 0)
                return new CommandResult { Ok = false, Error = "error: missing env name" };

            if (!await context.WslBackend.DistroExistsAsync(envName, cancellationToken))
                return new CommandResult { Ok = false, Error = $"error: WSL distro '{envName}' not found" };

            // Use WSL's --cd to start in the directory.
            var cdPath = startWindowsPath is null
                ? DefaultLinuxWkdir
                : PathUtils.WinPathToWslPath(startWindowsPath);

            context.Emit($"Launching shell for '{envName}'...");

            var wtPath = FindOnPath("wt.exe");
            if (wtPath is not null)
            {
                // wt.exe -- wsl.exe -d <env> --cd <path>
                var psi = new ProcessStartInfo
                {
                    FileName = wtPath,
                    UseShellExecute = true
                };

                psi.ArgumentList.Add("--");
                psi.ArgumentList.Add(context.WslBackend.ExePath);
                psi.ArgumentList.Add("-d");
                psi.ArgumentList.Add(envName);
                psi.ArgumentList.Add("--cd");
                psi.ArgumentList.Add(cdPath);

                Process.Start(psi);
            }
            else
            {
                // Fallback: cmd /c start "" wsl.exe -d <env> --cd <path>
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = true
                };

                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add("start");
                psi.ArgumentList.Add("");
                psi.ArgumentList.Add(context.WslBackend.ExePath);
                psi.ArgumentList.Add("-d");
                psi.ArgumentList.Add(envName);
                psi.ArgumentList.Add("--cd");
                psi.ArgumentList.Add(cdPath);

                Process.Start(psi);
            }

            return new CommandResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new CommandResult { Ok = false, Error = ex.Message };
        }
    }

    private static string? FindOnPath(string exeName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}