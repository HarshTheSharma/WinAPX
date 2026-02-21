using System.Diagnostics;

namespace WinAPX.Core;

public static class TerminalLauncher
{
    public static void LaunchInteractive(string exePath, IReadOnlyList<string> args)
    {
        var wtPath = FindOnPath("wt.exe");
        if (wtPath is not null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = wtPath,
                UseShellExecute = false
            };

            // wt <exe> <args...>
            startInfo.ArgumentList.Add(exePath);
            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            Process.Start(startInfo);
            return;
        }

        // Fallback: open in its own console window
        var cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        var joinedArgs = string.Join(" ", args.Select(Quote));

        var fallbackInfo = new ProcessStartInfo
        {
            FileName = cmdPath,
            UseShellExecute = false
        };

        // cmd /c start "" "<exePath>" <args...>
        fallbackInfo.ArgumentList.Add("/c");
        fallbackInfo.ArgumentList.Add("start");
        fallbackInfo.ArgumentList.Add("");
        fallbackInfo.ArgumentList.Add($"{Quote(exePath)} {joinedArgs}");

        Process.Start(fallbackInfo);
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

    private static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";

        if (value.Contains(' ') || value.Contains('"'))
            return "\"" + value.Replace("\"", "\\\"") + "\"";

        return value;
    }
}
