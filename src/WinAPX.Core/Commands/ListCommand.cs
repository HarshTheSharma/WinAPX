using Microsoft.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinAPX.Core.Commands;

public sealed class ListCommand : ICommand
{
    public Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var lxss = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
            if (lxss is null)
            {
                context.Emit("No WSL distros found.");
                return Task.FromResult(new CommandResult { Ok = true });
            }

            var subKeys = lxss.GetSubKeyNames();
            if (subKeys.Length == 0)
            {
                context.Emit("No WSL distros found.");
                return Task.FromResult(new CommandResult { Ok = true });
            }

            context.Emit("WSL distros (VHD path):");

            foreach (var subName in subKeys)
            {
                using var sub = lxss.OpenSubKey(subName);
                if (sub is null) continue;

                var name = sub.GetValue("DistributionName") as string;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var basePath = sub.GetValue("BasePath") as string;
                if (string.IsNullOrWhiteSpace(basePath))
                {
                    context.Emit(name);
                    continue;
                }

                var vhdx = Path.Combine(basePath, "ext4.vhdx");
                if (File.Exists(vhdx))
                    context.Emit($"{name} => {vhdx}");
                else
                    context.Emit($"{name} => {basePath}");
            }

            return Task.FromResult(new CommandResult { Ok = true });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CommandResult { Ok = false, Error = ex.Message });
        }
    }
}
