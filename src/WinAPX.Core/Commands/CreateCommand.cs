namespace WinAPX.Core.Commands;

public sealed class CreateCommand : ICommand
{
private readonly string envName;
    private readonly string? installLocation;
    private readonly DistroSpec distro;
    private readonly string? defaultDir;

    private readonly string defaultLinuxUser;

    public CreateCommand(string envName, string? installLocation = null, DistroSpec? distro = null, string? defaultDir = null)
    {
        this.envName = PathUtils.CleanName(envName);
        if (string.IsNullOrWhiteSpace(installLocation))
            this.installLocation = null;
        else
            this.installLocation = installLocation;
        if (distro is null)
            this.distro = DistroSpec.Ubuntu;
        else
            this.distro = distro;
        if (string.IsNullOrWhiteSpace(defaultDir))
            this.defaultDir = null;
        else
            this.defaultDir = defaultDir;

        var raw = this.envName.ToLowerInvariant();
        var filtered = new string(raw.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(filtered))
            defaultLinuxUser = "winapx";
        else
            defaultLinuxUser = filtered;
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

            context.Emit($"Ensuring base {distro.DisplayName} tar exists...");
            var baseTarPath = ApxPaths.BaseTarPath(distro.Id);
            await context.WslBackend.EnsureBaseTarAsync(
                distro,
                baseTarPath,
                msg => context.Emit(msg),
                cancellationToken);

            var instanceDir = installLocation is null
                ? ApxPaths.InstanceDir(envName)
                : Path.Combine(installLocation, envName);

            Directory.CreateDirectory(instanceDir);

            if (Directory.EnumerateFileSystemEntries(instanceDir).Any())
                return new CommandResult { Ok = false, Error = $"error: instance dir not empty: {instanceDir}" };

            context.Emit($"Importing '{envName}' from base {distro.DisplayName}...");
            var exitCode = await context.WslBackend.RunAsync(
                new[] { "--import", envName, instanceDir, baseTarPath },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: wsl --import failed" };

            context.Emit($"Provisioning default user '{defaultLinuxUser}'...");
            exitCode = await context.WslBackend.RunAsync(
                new[]
                {
                    "-d", envName, "-u", "root", "--", "bash", "-lc",
                    $"id -u {defaultLinuxUser} >/dev/null 2>&1 || useradd -m -d /home/{defaultLinuxUser} -s /bin/bash {defaultLinuxUser}"
                },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: failed to create default user" };

            // create with passwordless sudo
            exitCode = await context.WslBackend.RunAsync(
                new[]
                {
                    "-d", envName, "-u", "root", "--", "bash", "-lc",
                    $"mkdir -p /etc/sudoers.d && echo '{defaultLinuxUser} ALL=(ALL) NOPASSWD:ALL' > /etc/sudoers.d/{defaultLinuxUser} && chmod 440 /etc/sudoers.d/{defaultLinuxUser}"
                },
                _ => { },
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: failed to configure sudoers" };

            exitCode = await context.WslBackend.RunAsync(
                new[]
                {
                    "-d", envName, "-u", "root", "--", "bash", "-lc",
                    $"printf '[user]\\ndefault={defaultLinuxUser}\\n' > /etc/wsl.conf"
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

            // Copy shortcut icon next to the VHDX
            var iconStream = typeof(CreateCommand).Assembly
                .GetManifestResourceStream("WinAPX.Core.shortcut.ico");
            if (iconStream is not null)
            {
                using (iconStream)
                {
                    var iconDest = Path.Combine(instanceDir, "shortcut.ico");
                    using var fs = File.Create(iconDest);
                    await iconStream.CopyToAsync(fs, cancellationToken);
                }
            }

            // Save wkdir if provided and set it up
            if (defaultDir is not null)
            {
                Directory.CreateDirectory(defaultDir);
                Directory.CreateDirectory(ApxPaths.MetaDir(envName));
                await File.WriteAllTextAsync(ApxPaths.DefaultDirFile(envName), defaultDir, cancellationToken);

                var wkdirLinux = PathUtils.WinPathToWslPath(defaultDir);

                // make the ~/wkdir symlink
                await context.WslBackend.RunAsync(
                    new[]
                    {
                        "-d", envName, "-u", "root", "--", "bash", "-lc",
                        $"ln -sfn '{wkdirLinux}' /home/{defaultLinuxUser}/wkdir && " +
                        $"chown -h {defaultLinuxUser}:{defaultLinuxUser} /home/{defaultLinuxUser}/wkdir"
                    },
                    _ => { },
                    e => context.Emit("[err] " + e),
                    cancellationToken);

                // export $WKDIR env var
                await context.WslBackend.RunAsync(
                    new[]
                    {
                        "-d", envName, "-u", "root", "--", "bash", "-lc",
                        $"printf 'export WKDIR={wkdirLinux}\\n' > /etc/profile.d/winapx.sh"
                    },
                    _ => { },
                    e => context.Emit("[err] " + e),
                    cancellationToken);

                context.Emit($"Default working dir set to: {defaultDir}");
            }

            context.Emit("Done.");
            return new CommandResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new CommandResult { Ok = false, Error = ex.Message };
        }
    }
}