using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinAPX.Core.Commands;

public sealed class EnterCommand : ICommand
{
private readonly string envName;
    private readonly string? startWindowsPath;
    private readonly bool newWindow;

    public EnterCommand(string envName, string? startWindowsPath = null, bool newWindow = false)
    {
        this.envName = PathUtils.CleanName(envName);
        if (string.IsNullOrWhiteSpace(startWindowsPath))
            this.startWindowsPath = null;
        else
            this.startWindowsPath = startWindowsPath;
        this.newWindow = newWindow;
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (envName.Length == 0)
                return new CommandResult { Ok = false, Error = "error: missing env name" };

            if (!await context.WslBackend.DistroExistsAsync(envName, cancellationToken))
                return new CommandResult { Ok = false, Error = $"error: WSL distro '{envName}' not found" };

            // use the starting directory
            string cdPath;
            if (startWindowsPath is not null)
            {
                cdPath = PathUtils.WinPathToWslPath(startWindowsPath);
            }
            else
            {
                var defaultDirFile = ApxPaths.DefaultDirFile(envName);
                if (File.Exists(defaultDirFile))
                {
                    var stored = (await File.ReadAllTextAsync(defaultDirFile, cancellationToken)).Trim();
                    if (stored.Length > 0)
                        cdPath = PathUtils.WinPathToWslPath(stored);
                    else
                        cdPath = "~";
                }
                else
                {
                    cdPath = "~";
                }
            }

            if (newWindow)
            {
                context.Emit($"Launching shell for '{envName}' in new window...");

                var wtPath = FindOnPath("wt.exe");
                if (wtPath is not null)
                {
                    var psi = new ProcessStartInfo { FileName = wtPath, UseShellExecute = true };
                    psi.ArgumentList.Add("new-tab");
                    psi.ArgumentList.Add(context.WslBackend.ExePath);
                    psi.ArgumentList.Add("-d");
                    psi.ArgumentList.Add(envName);
                    psi.ArgumentList.Add("--cd"); psi.ArgumentList.Add(cdPath);
                    Process.Start(psi);
                }
                else
                {
                    var psi = new ProcessStartInfo { FileName = "cmd.exe", UseShellExecute = true };
                    psi.ArgumentList.Add("/c");
                    psi.ArgumentList.Add("start");
                    psi.ArgumentList.Add("");
                    psi.ArgumentList.Add(context.WslBackend.ExePath);
                    psi.ArgumentList.Add("-d");
                    psi.ArgumentList.Add(envName);
                    psi.ArgumentList.Add("--cd"); psi.ArgumentList.Add(cdPath);
                    Process.Start(psi);
                }
            }
            else
            {
                // Run inline in the current terminal
                var psi = new ProcessStartInfo { FileName = context.WslBackend.ExePath, UseShellExecute = false };
                psi.ArgumentList.Add("-d");
                psi.ArgumentList.Add(envName);
                psi.ArgumentList.Add("--cd"); psi.ArgumentList.Add(cdPath);

                var proc = Process.Start(psi)!;
                await proc.WaitForExitAsync(cancellationToken);
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