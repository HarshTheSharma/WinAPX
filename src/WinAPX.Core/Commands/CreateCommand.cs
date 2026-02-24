using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WinAPX.Core.Commands;

public sealed class CreateCommand : ICommand
{
    public string Name => "create";
    private readonly string envName;
    private readonly string? installLocation;

    private const string DefaultLinuxUser = "winapx";

    public CreateCommand(string envName, string? installLocation = null)
    {
        this.envName = PathUtils.CleanName(envName);
        this.installLocation = string.IsNullOrWhiteSpace(installLocation) ? null : installLocation;
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (envName.Length == 0)
                return new CommandResult { Ok = false, Error = "error: missing env name" };

            ApxPaths.EnsureBaseDirs();

            if (await context.WslBackend.DistroExistsAsync(envName, cancellationToken))
                return new CommandResult { Ok = false, Error = $"error: WSL distro '{envName}' already exists" };

            context.Emit("Ensuring base Ubuntu tar exists...");
            await context.WslBackend.EnsureUbuntuBaseTarAsync(
                ApxPaths.UbuntuBaseTarPath,
                msg => context.Emit(msg),
                cancellationToken);

            // If a folder was chosen in the GUI, import into that folder so ext4.vhdx lives there.
            // Use a per-env subfolder inside the chosen folder.
            var instanceDir = installLocation is null
                ? ApxPaths.InstanceDir(envName)
                : Path.Combine(installLocation, envName);

            Directory.CreateDirectory(instanceDir);

            if (Directory.EnumerateFileSystemEntries(instanceDir).Any())
                return new CommandResult { Ok = false, Error = $"error: instance dir not empty: {instanceDir}" };

            context.Emit($"Importing '{envName}' from base Ubuntu...");
            var exitCode = await context.WslBackend.RunAsync(
                new[] { "--import", envName, instanceDir, ApxPaths.UbuntuBaseTarPath },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: wsl --import failed" };

            // Provision: create Linux user home inside VHDX and set default user.
            context.Emit($"Provisioning default user '{DefaultLinuxUser}'...");
            exitCode = await context.WslBackend.RunAsync(
                new[]
                {
                    "-d", envName, "-u", "root", "--", "bash", "-lc",
                    $"id -u {DefaultLinuxUser} >/dev/null 2>&1 || useradd -m -d /home/{DefaultLinuxUser} -s /bin/bash {DefaultLinuxUser}"
                },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: failed to create default user" };

            exitCode = await context.WslBackend.RunAsync(
                new[]
                {
                    "-d", envName, "-u", "root", "--", "bash", "-lc",
                    $"printf '[user]\\ndefault={DefaultLinuxUser}\\n' > /etc/wsl.conf"
                },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: failed to write /etc/wsl.conf" };

            await context.WslBackend.RunAsync(
                new[] { "--terminate", envName },
                _ => { },
                e => context.Emit("[err] " + e),
                cancellationToken);

            context.Emit("Done.");
            return new CommandResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new CommandResult { Ok = false, Error = ex.Message };
        }
    }
}