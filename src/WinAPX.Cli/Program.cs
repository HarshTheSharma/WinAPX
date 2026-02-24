using WinAPX.Core;
using WinAPX.Core.Commands;

static void PrintUsage()
{
    Console.WriteLine("winapx create <name> [--installDir <windowsPath>] [--homeDir <windowsPath>]");
    Console.WriteLine("winapx enter <name>");
}

if (args.Length == 0)
{
    PrintUsage();
    return;
}

var dispatcher = new CommandDispatcher();
using var cancellationTokenSource = new CancellationTokenSource();
var cancellationToken = cancellationTokenSource.Token;

CommandResult result;

switch (args[0].ToLowerInvariant())
{
    case "create":
    {
        if (args.Length < 2)
        {
            PrintUsage();
            return;
        }

        var name = args[1];
        string? installDir = null;

        for (var i = 2; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--installdir", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[i], "--installDir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    installDir = args[++i];
                }
            }
            // Back-compat: treat --homeDir as install location
            else if (string.Equals(args[i], "--homedir", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(args[i], "--homeDir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    installDir = args[++i];
                }
            }
        }

        var command = new CreateCommand(name, installDir);
        result = await dispatcher.RunAsync(command, e => Console.WriteLine($"[{e.At:HH:mm:ss}] {e.Message}"), cancellationToken);
        break;
    }

    case "enter":
    {
        if (args.Length < 2)
        {
            PrintUsage();
            return;
        }

        var command = new EnterCommand(args[1]);
        result = await dispatcher.RunAsync(command, e => Console.WriteLine($"[{e.At:HH:mm:ss}] {e.Message}"), cancellationToken);
        break;
    }

    default:
        PrintUsage();
        return;
}

if (!result.Ok)
{
    Console.Error.WriteLine(result.Error ?? "error");
    Environment.Exit(result.ExitCode ?? 1);
}