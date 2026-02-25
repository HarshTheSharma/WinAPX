using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinAPX.Core.Commands;

public sealed class ListCommand : ICommand
{
    public string Name => "list";

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            var distros = new List<string>();

            var exitCode = await context.WslBackend.RunAsync(
                new[] { "-l", "-q" },
                s =>
                {
                    var line = (s ?? "").Trim();
                    if (line.Length > 0)
                        distros.Add(line);
                },
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: wsl -l -q failed" };

            if (distros.Count == 0)
            {
                context.Emit("No WSL distros found.");
                return new CommandResult { Ok = true };
            }

            context.Emit("WSL distros (VHD path):");

            foreach (var name in distros)
            {
                {
                    var s = (name ?? "").Replace("\r", "").Replace("\n", "");
                    bool hasAlnum = false;
                    for (int i = 0; i < s.Length; i++)
                        if (char.IsLetterOrDigit(s[i])) { hasAlnum = true; break; }
                    if (!hasAlnum) continue;
                }
                var info = TryGetWslInfo(name);

                if (info is null)
                {
                    context.Emit($"{name}");
                    continue;
                }

                // WSL2 stores filesystem in ext4.vhdx under BasePath.
                // WSL1 has no VHDX.
                if (info.Version == 2)
                {
                    var vhdx = Path.Combine(info.BasePath, "ext4.vhdx");
                    if (File.Exists(vhdx)) 
                    {
                        context.Emit($"{name} => {vhdx}");
                        continue;
                    }
                }
                context.Emit($"{name}");
            }

            return new CommandResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new CommandResult { Ok = false, Error = ex.Message };
        }
    }

    private sealed class WslInfo
    {
        public required string BasePath { get; init; }
        public required int Version { get; init; }
    }

    private static WslInfo? TryGetWslInfo(string distroName)
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

            var basePath = sub.GetValue("BasePath") as string;
            if (string.IsNullOrWhiteSpace(basePath))
                return null;

            var versionObj = sub.GetValue("Version");
            var version = 0;
            if (versionObj is int vi) version = vi;
            else if (versionObj is long vl) version = (int)vl;

            return new WslInfo { BasePath = basePath, Version = version };
        }

        return null;
    }
}