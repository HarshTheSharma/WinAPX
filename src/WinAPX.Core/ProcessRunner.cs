using System.Diagnostics;
using System.Text;

namespace WinAPX.Core;

public sealed class ProcessRunner
{
    public async Task<int> RunAsync(
        string fileName,
        IReadOnlyList<string> args,
        Action<string> onStdout,
        Action<string> onStderr,
        CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in args)
            processStartInfo.ArgumentList.Add(arg);

        using var process = new Process
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true
        };

        process.Start();

        async Task PumpAsync(StreamReader reader, Action<string> sink)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null) break;
                sink(line);
            }
        }

        var pumpOutTask = PumpAsync(process.StandardOutput, onStdout);
        var pumpErrTask = PumpAsync(process.StandardError, onStderr);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(pumpOutTask, pumpErrTask).ConfigureAwait(false);

        return process.ExitCode;
    }
}