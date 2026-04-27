namespace WinAPX.Core.Commands;

public sealed class CreateCommand : ICommand
{
    internal static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "list", "create", "enter", "delete", "export", "import",
        "help", "--help", "-h", "info", "version"
    };

    private readonly string envName;
    private readonly string? installLocation;
    private readonly DistroSpec distro;
    private readonly string? defaultDir;
    private readonly string? packages;
    private readonly bool installRecommendedPkgs;

    private readonly string defaultLinuxUser;

    public CreateCommand(string envName, string? installLocation = null, DistroSpec? distro = null, string? defaultDir = null, string? packages = null, bool installRecommendedPkgs = false)
    {
        this.envName = ApxPaths.CleanName(envName);
        this.installLocation = string.IsNullOrWhiteSpace(installLocation) ? null : installLocation;
        this.distro = distro ?? DistroSpec.Ubuntu;
        this.defaultDir = string.IsNullOrWhiteSpace(defaultDir) ? null : defaultDir;
        this.packages = string.IsNullOrWhiteSpace(packages) ? null : packages.Trim();
        this.installRecommendedPkgs = installRecommendedPkgs;

        var raw = this.envName.ToLowerInvariant();
        var filtered = new string(raw.Where(char.IsLetterOrDigit).ToArray());
        defaultLinuxUser = string.IsNullOrWhiteSpace(filtered) ? "winapx" : filtered;
    }

    public async Task<CommandResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (envName.Length == 0)
            {
                return new CommandResult { Ok = false, Error = "error: missing env name" };
            }

            if (ReservedNames.Contains(envName))
            {
                return new CommandResult { Ok = false, Error = $"error: '{envName}' is a reserved command name; pick another" };
            }

            ApxPaths.EnsureBaseDirs();

            if (await context.WslBackend.DistroExistsAsync(envName, cancellationToken))
            {
                return new CommandResult { Ok = false, Error = $"error: WSL distro '{envName}' already exists" };
            }

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
            {
                return new CommandResult { Ok = false, Error = $"error: instance dir not empty: {instanceDir}" };
            }

            context.Emit($"Importing '{envName}' from base {distro.DisplayName}...");
            var exitCode = await context.WslBackend.RunAsync(
                new[] { "--import", envName, instanceDir, baseTarPath },
                s => context.Emit(s),
                e => context.Emit("[err] " + e),
                cancellationToken);

            if (exitCode != 0)
            {
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: wsl --import failed" };
            }

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
            {
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: failed to create default user" };
            }

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
            {
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: failed to configure sudoers" };
            }

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
            {
                return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: failed to write /etc/wsl.conf" };
            }

            if (installRecommendedPkgs)
            {
                context.Emit("Installing recommended packages...");
                var recommendedPkgs = distro.Id switch
                {
                    "arch" => "sudo curl wget git ca-certificates base-devel gcc",
                    _      => "sudo curl wget git ca-certificates build-essential gcc"
                };
                var recommendedCmd = distro.Id switch
                {
                    "arch" => $"pacman -Sy --noconfirm {recommendedPkgs}",
                    _      => $"apt-get update -y && apt-get install -y {recommendedPkgs}"
                };
                exitCode = await context.WslBackend.RunAsync(
                    new[]
                    {
                        "-d", envName, "-u", "root", "--", "bash", "-lc", recommendedCmd
                    },
                    s => context.Emit(s),
                    e => context.Emit("[err] " + e),
                    cancellationToken);

                if (exitCode != 0)
                {
                    return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: recommended package installation failed" };
                }
            }

            if (packages is not null)
            {
                context.Emit($"Installing packages: {packages}");
                var installCmd = distro.Id switch
                {
                    "arch" => $"pacman -Sy --noconfirm {packages}",
                    _      => $"apt-get update -y && apt-get install -y {packages}"
                };
                exitCode = await context.WslBackend.RunAsync(
                    new[]
                    {
                        "-d", envName, "-u", "root", "--", "bash", "-lc", installCmd
                    },
                    s => context.Emit(s),
                    e => context.Emit("[err] " + e),
                    cancellationToken);

                if (exitCode != 0)
                {
                    return new CommandResult { Ok = false, ExitCode = exitCode, Error = "error: package installation failed" };
                }

                Directory.CreateDirectory(ApxPaths.MetaDir(envName));
                await File.WriteAllTextAsync(ApxPaths.PackagesFile(envName), packages, cancellationToken);
            }

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

                var wkdirLinux = ApxPaths.WinPathToWslPath(defaultDir);

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