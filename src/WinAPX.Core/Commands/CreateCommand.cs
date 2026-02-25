namespace WinAPX.Core.Commands;

public sealed class CreateCommand : ICommand
{
    public string Name => "create";
    private readonly string envName;
    private readonly string? installLocation;

    private readonly string defaultLinuxUser;
    private const string LinuxWkdir = "/wkdir";

    public CreateCommand(string envName, string? installLocation = null)
    {
        this.envName = PathUtils.CleanName(envName);
        this.installLocation = string.IsNullOrWhiteSpace(installLocation) ? null : installLocation;

        var raw = this.envName.ToLowerInvariant();
        var filtered = new string(raw.Where(char.IsLetterOrDigit).ToArray());
        this.defaultLinuxUser = string.IsNullOrWhiteSpace(filtered) ? "winapx" : filtered;
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

            var instanceDir = installLocation is null
                ? ApxPaths.InstanceDir(envName)
                : Path.Combine(installLocation, envName);

            Directory.CreateDirectory(instanceDir);

            if (Directory.EnumerateFileSystemEntries(instanceDir).Any())
                return new CommandResult { Ok = false, Error = $"error: instance dir not empty: {instanceDir}" };

            // Create wkdir next to the VHDX (inside instanceDir).
            var wkdirWin = Path.Combine(instanceDir, "wkdir");
            Directory.CreateDirectory(wkdirWin);

            context.Emit($"Importing '{envName}' from base Ubuntu...");
            var exitCode = await context.WslBackend.RunAsync(
                new[] { "--import", envName, instanceDir, ApxPaths.UbuntuBaseTarPath },
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

            exitCode = await context.WslBackend.RunAsync(
                new[]
                {
                    "-d", envName, "-u", "root", "--", "bash", "-lc",
                    $"printf '[user]\\ndefault={defaultLinuxUser}\\n\\n[automount]\\nenabled=true\\nmountFsTab=true\\n' > /etc/wsl.conf"
                },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: failed to write /etc/wsl.conf" };

            // Mount wkdir (Windows folder) into Linux at /wkdir via /etc/fstab.
            var wkdirFstabPath = PathUtils.WinPathToDrvfsFstabPath(wkdirWin);
            var fstabLine = $"{wkdirFstabPath} {LinuxWkdir} drvfs metadata,uid=1000,gid=1000,umask=022 0 0";

            exitCode = await context.WslBackend.RunAsync(
                new[]
                {
                    "-d", envName, "-u", "root", "--", "bash", "-lc",
                    $"mkdir -p {LinuxWkdir} && cat > /etc/fstab <<'EOF'\n{fstabLine}\nEOF"
                },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: failed to write /etc/fstab for wkdir" };

            // Export WKDIR globally for shells.
            exitCode = await context.WslBackend.RunAsync(
                new[]
                {
                    "-d", envName, "-u", "root", "--", "bash", "-lc",
                    $"printf 'export WKDIR={LinuxWkdir}\\n' > /etc/profile.d/winapx-wkdir.sh"
                },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: failed to write WKDIR profile script" };

            // Convenience link in home: ~/wkdir -> /wkdir
            await context.WslBackend.RunAsync(
                new[]
                {
                    "-d", envName, "-u", "root", "--", "bash", "-lc",
                    $"ln -sfn {LinuxWkdir} /home/{defaultLinuxUser}/wkdir && chown -h {defaultLinuxUser}:{defaultLinuxUser} /home/{defaultLinuxUser}/wkdir"
                },
                _ => { },
                e => context.Emit("[err] " + e),
                cancellationToken);

            // Terminate so wsl.conf + fstab apply next launch.
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