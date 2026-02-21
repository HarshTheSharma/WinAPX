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
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line is not null) sink(line);
            }
        }

        var pumpOutTask = PumpAsync(process.StandardOutput, onStdout);
        var pumpErrTask = PumpAsync(process.StandardError, onStderr);

        await Task.WhenAll(pumpOutTask, pumpErrTask);
        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode;
    }
}
