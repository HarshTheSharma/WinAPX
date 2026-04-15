using WinAPX.Core;
using WinAPX.Core.Commands;

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  winapx list");
    Console.WriteLine("  winapx create <name> [--distro <id>] [--install-dir <windowsPath>] [--wkdir <windowsPath>] [--packages \"<pkg1> <pkg2>...\"] [--install-recommended-pkgs]");
    Console.WriteLine("  winapx enter <name> [--dir <windowsPath>] [--new-window]");
    Console.WriteLine("  winapx delete <name> [--keep-files]");
    Console.WriteLine("  winapx export <name> <outputFile.tar>");
    Console.WriteLine("  winapx import <name> <tarFile> [--install-dir <windowsPath>]");
    Console.WriteLine();
    Console.WriteLine("Distros: ubuntu, arch");
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
    case "list":
    {
        var command = new ListCommand();
        result = await dispatcher.RunAsync(command, e => Console.WriteLine($"[{e.At:HH:mm:ss}] {e.Message}"), cancellationToken);
        break;
    }

    case "create":
    {
        if (args.Length < 2)
        {
            PrintUsage();
            return;
        }

        var name = args[1];
        string? installDir = null;
        string? wkdir = null;
        string? packages = null;
        var installRecommendedPkgs = false;
        DistroSpec? distro = null;

        for (var i = 2; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--install-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                    installDir = args[++i];
            }
            else if (string.Equals(args[i], "--wkdir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                    wkdir = args[++i];
            }
            else if (string.Equals(args[i], "--packages", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                    packages = args[++i];
            }
            else if (string.Equals(args[i], "--install-recommended-pkgs", StringComparison.OrdinalIgnoreCase))
            {
                installRecommendedPkgs = true;
            }
            else if (string.Equals(args[i], "--distro", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    var distroId = args[++i];
                    distro = DistroSpec.Find(distroId);
                    if (distro is null)
                    {
                        Console.Error.WriteLine($"error: unknown distro '{distroId}'. Valid options: ubuntu, arch");
                        Environment.Exit(1);
                        return;
                    }
                }
            }
        }

        var command = new CreateCommand(name, installDir, distro, wkdir, packages, installRecommendedPkgs);
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

        string? dir = null;
        var newWindow = false;

        for (var i = 2; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                    dir = args[++i];
            }
            else if (string.Equals(args[i], "--new-window", StringComparison.OrdinalIgnoreCase))
            {
                newWindow = true;
            }
        }

        var command = new EnterCommand(args[1], dir, newWindow);
        result = await dispatcher.RunAsync(command, e => Console.WriteLine($"[{e.At:HH:mm:ss}] {e.Message}"), cancellationToken);
        break;
    }

    case "delete":
    {
        if (args.Length < 2)
        {
            PrintUsage();
            return;
        }

        var keepFiles = false;
        for (var i = 2; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--keep-files", StringComparison.OrdinalIgnoreCase))
                keepFiles = true;
        }

        var command = new DeleteCommand(args[1], keepFiles);
        result = await dispatcher.RunAsync(command, e => Console.WriteLine($"[{e.At:HH:mm:ss}] {e.Message}"), cancellationToken);
        break;
    }

    case "export":
    {
        if (args.Length < 3)
        {
            PrintUsage();
            return;
        }

        var command = new ExportCommand(args[1], args[2]);
        result = await dispatcher.RunAsync(command, e => Console.WriteLine($"[{e.At:HH:mm:ss}] {e.Message}"), cancellationToken);
        break;
    }

    case "import":
    {
        if (args.Length < 3)
        {
            PrintUsage();
            return;
        }

        string? installDir = null;

        for (var i = 3; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--install-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                    installDir = args[++i];
            }
        }

        var command = new ImportCommand(args[1], args[2], installDir);
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
