namespace WinAPX.Core;

public sealed class WslBackend
{
    private readonly ProcessRunner processRunner = new();

    public string ExePath { get; }

    public WslBackend()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system32 = Path.Combine(winDir, "System32", "wsl.exe");

        if (File.Exists(system32))
        {
            ExePath = system32;
        }
        else
        {
            ExePath = "wsl.exe";
        }
    }

    public Task<int> RunAsync(
        IReadOnlyList<string> args,
        Action<string> onStdout,
        Action<string> onStderr,
        CancellationToken cancellationToken)
    {
        return processRunner.RunAsync(ExePath, args, onStdout, onStderr, cancellationToken);
    }

    public async Task<(int exitCode, List<string> stdout, List<string> stderr)> RunCaptureAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var outLines = new List<string>();
        var errLines = new List<string>();

        var exitCode = await RunAsync(
            args,
            s => outLines.Add(s),
            e => errLines.Add(e),
            cancellationToken);

        return (exitCode, outLines, errLines);
    }

    public async Task<List<string>> ListDistrosQuietAsync(CancellationToken cancellationToken)
    {
        var (exitCode, stdout, stderr) = await RunCaptureAsync(new[] { "-l", "-q" }, cancellationToken);

        // If this is empty it usually means WSL failed to run
        var names = stdout
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        return names;
    }

    public async Task<bool> DistroExistsAsync(string distroName, CancellationToken cancellationToken)
    {
        var name = (distroName ?? "").Trim();
        if (name.Length == 0) return false;

        var distros = await ListDistrosQuietAsync(cancellationToken);
        if (distros.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
            return true;

        var (exitCode, _, err) = await RunCaptureAsync(
            new[] { "-d", name, "--", "true" },
            cancellationToken);

        if (exitCode == 0) return true;

        var combinedErr = string.Join("\n", err);
        if (combinedErr.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
            combinedErr.Contains("is not installed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // WSL failed for some other reason
        return false;
    }

    public async Task<bool> EnsureDistroAppearsAsync(string distroName, int tries, CancellationToken cancellationToken)
    {
        for (var i = 0; i < tries; i++)
        {
            if (await DistroExistsAsync(distroName, cancellationToken)) return true;
            await Task.Delay(1000, cancellationToken);
        }

        await RunAsync(new[] { "--shutdown" }, _ => { }, _ => { }, cancellationToken);
        await Task.Delay(1000, cancellationToken);

        for (var i = 0; i < tries; i++)
        {
            if (await DistroExistsAsync(distroName, cancellationToken)) return true;
            await Task.Delay(1000, cancellationToken);
        }

        return false;
    }

    public async Task<bool> SupportsCdAsync(CancellationToken cancellationToken)
    {
        var (exitCode, stdout, _) = await RunCaptureAsync(new[] { "--help" }, cancellationToken);
        if (exitCode != 0) return false;

        return string.Join("\n", stdout).Contains("--cd", StringComparison.OrdinalIgnoreCase);
    }

    public Task EnsureUbuntuInstalledAsync(Action<string> log, CancellationToken cancellationToken)
    {
        // Again need to remove this hardcode later
        log("Assuming Ubuntu is already installed.");
        return Task.CompletedTask;
    }

    public async Task EnsureUbuntuBaseTarAsync(string ubuntuBaseTarPath, Action<string> log, CancellationToken cancellationToken)
    {
        if (File.Exists(ubuntuBaseTarPath))
        {
            log($"Base Ubuntu tar already exists: {ubuntuBaseTarPath}");
            return;
        }

        await EnsureUbuntuInstalledAsync(log, cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(ubuntuBaseTarPath)!);

        // Need to remove this hardcode later
        var ubuntuDistroName = "Ubuntu";

        var ubuntuExists = await DistroExistsAsync(ubuntuDistroName, cancellationToken);
        if (!ubuntuExists)
        {
            // WSL is failing to run correctly from the app.
            throw new InvalidOperationException(
                "Ubuntu was not detected by the app. WSL may be failing to run from the app process.");
        }

        log($"Exporting '{ubuntuDistroName}' to base tar: {ubuntuBaseTarPath}");

        var (exitCode, _, err) = await RunCaptureAsync(
            new[] { "--export", ubuntuDistroName, ubuntuBaseTarPath },
            cancellationToken);

        if (exitCode != 0)
        {
            var combinedErr = string.Join("\n", err);
            throw new InvalidOperationException("Failed to export Ubuntu base tar:\n" + combinedErr);
        }
    }

    public async Task<string> SelfTestAsync(CancellationToken cancellationToken)
    {
        var (exitCode, stdout, stderr) = await RunCaptureAsync(new[] { "-l", "-q" }, cancellationToken);

        var s1 = "wsl -l -q exitCode=" + exitCode;
        var s2 = "stdout:\n" + string.Join("\n", stdout);
        var s3 = "stderr:\n" + string.Join("\n", stderr);

        return s1 + "\n\n" + s2 + "\n\n" + s3;
    }
}
