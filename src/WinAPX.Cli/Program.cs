using WinAPX.Core;
using WinAPX.Core.Commands;

var TopLevelVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "list", "create", "enter", "delete", "export", "import",
    "help", "--help", "-h"
};

static void PrintUsage()
{
    Console.WriteLine("Usage (environment operations):");
    Console.WriteLine("  winapx list");
    Console.WriteLine("  winapx create <env> [--distro <id>] [--install-dir <path>] [--wkdir <path>]");
    Console.WriteLine("                      [--packages \"<p1> <p2>...\"] [--install-recommended-pkgs]");
    Console.WriteLine("  winapx enter  <env> [--dir <path>] [--new-window]");
    Console.WriteLine("  winapx delete <env> [--keep-files]");
    Console.WriteLine("  winapx export <env> <out.tar>");
    Console.WriteLine("  winapx import <name> <tar> [--install-dir <path>]");
    Console.WriteLine();
    Console.WriteLine("Usage (app operations within an env):");
    Console.WriteLine("  winapx <env> list");
    Console.WriteLine("  winapx <env> export <displayName> --cmd \"<command>\"");
    Console.WriteLine("                                    [--icon <path>] [--output-dir <dir>] [--no-launch]");
    Console.WriteLine("  winapx <env> delete <appId>");
    Console.WriteLine("  winapx <env> launch <appId>");
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

static void Emit(CommandEvent e) => Console.WriteLine($"[{e.At:HH:mm:ss}] {e.Message}");

// Escape hatch: `winapx -- <env> <verb> ...`
var parseArgs = args;
var forceEnvScoped = false;
if (args[0] == "--")
{
    parseArgs = args[1..];
    forceEnvScoped = true;
}

if (parseArgs.Length == 0)
{
    PrintUsage();
    return;
}

CommandResult result;
if (!forceEnvScoped && TopLevelVerbs.Contains(parseArgs[0]))
    result = await RunTopLevelAsync(parseArgs, dispatcher, cancellationToken);
else
    result = await RunEnvScopedAsync(parseArgs, dispatcher, cancellationToken);

if (!result.Ok)
{
    Console.Error.WriteLine(result.Error ?? "error");
    Environment.Exit(result.ExitCode ?? 1);
}

static async Task<CommandResult> RunTopLevelAsync(string[] args, CommandDispatcher dispatcher, CancellationToken ct)
{
    switch (args[0].ToLowerInvariant())
    {
        case "list":
        {
            var command = new ListCommand();
            return await dispatcher.RunAsync(command, Emit, ct);
        }

        case "create":
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return new CommandResult { Ok = true };
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
                    installDir = i + 1 < args.Length ? args[++i] : installDir;
                }
                else if (string.Equals(args[i], "--wkdir", StringComparison.OrdinalIgnoreCase))
                {
                    wkdir = i + 1 < args.Length ? args[++i] : wkdir;
                }
                else if (string.Equals(args[i], "--packages", StringComparison.OrdinalIgnoreCase))
                {
                    packages = i + 1 < args.Length ? args[++i] : packages;
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
                            return new CommandResult { Ok = false };
                        }
                    }
                }
            }

            var command = new CreateCommand(name, installDir, distro, wkdir, packages, installRecommendedPkgs);
            return await dispatcher.RunAsync(command, Emit, ct);
        }

        case "enter":
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return new CommandResult { Ok = true };
            }

            string? dir = null;
            var newWindow = false;

            for (var i = 2; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--dir", StringComparison.OrdinalIgnoreCase))
                {
                    dir = i + 1 < args.Length ? args[++i] : dir;
                }
                else if (string.Equals(args[i], "--new-window", StringComparison.OrdinalIgnoreCase))
                {
                    newWindow = true;
                }
            }

            var command = new EnterCommand(args[1], dir, newWindow);
            return await dispatcher.RunAsync(command, Emit, ct);
        }

        case "delete":
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return new CommandResult { Ok = true };
            }

            var keepFiles = false;
            for (var i = 2; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--keep-files", StringComparison.OrdinalIgnoreCase))
                {
                    keepFiles = true;
                }
            }

            var command = new DeleteCommand(args[1], keepFiles);
            return await dispatcher.RunAsync(command, Emit, ct);
        }

        case "export":
        {
            if (args.Length < 3)
            {
                PrintUsage();
                return new CommandResult { Ok = true };
            }

            var command = new ExportEnvCommand(args[1], args[2]);
            return await dispatcher.RunAsync(command, Emit, ct);
        }

        case "import":
        {
            if (args.Length < 3)
            {
                PrintUsage();
                return new CommandResult { Ok = true };
            }

            string? installDir = null;
            for (var i = 3; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--install-dir", StringComparison.OrdinalIgnoreCase))
                {
                    installDir = i + 1 < args.Length ? args[++i] : installDir;
                }
            }

            var command = new ImportEnvCommand(args[1], args[2], installDir);
            return await dispatcher.RunAsync(command, Emit, ct);
        }

        case "help":
        case "--help":
        case "-h":
            PrintUsage();
            return new CommandResult { Ok = true };

        default:
            PrintUsage();
            return new CommandResult { Ok = true };
    }
}

static async Task<CommandResult> RunEnvScopedAsync(string[] args, CommandDispatcher dispatcher, CancellationToken ct)
{
    var envName = args[0];
    if (args.Length < 2)
    {
        return await dispatcher.RunAsync(new ListAppsCommand(envName), Emit, ct);
    }

    var verb = args[1].ToLowerInvariant();
    ICommand command;
    switch (verb)
    {
        case "list":
            command = new ListAppsCommand(envName);
            break;

        case "export":
        {
            if (args.Length < 3)
            {
                return new CommandResult { Ok = false, Error = "error: missing app display name" };
            }

            var displayName = args[2];
            string? cmd = null, icon = null, outDir = null;
            var launch = true;

            for (var i = 3; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--cmd" when i + 1 < args.Length:        cmd    = args[++i]; break;
                    case "--icon" when i + 1 < args.Length:       icon   = args[++i]; break;
                    case "--output-dir" when i + 1 < args.Length: outDir = args[++i]; break;
                    case "--no-launch":                           launch = false;     break;
                }
            }

            if (cmd is null)
            {
                return new CommandResult { Ok = false, Error = "error: --cmd is required" };
            }

            command = new ExportAppCommand(envName, displayName, cmd, icon, outDir, launch);
            break;
        }

        case "delete":
        {
            if (args.Length < 3)
            {
                return new CommandResult { Ok = false, Error = "error: missing app id" };
            }
            command = new DeleteAppCommand(envName, args[2]);
            break;
        }

        case "launch":
        {
            if (args.Length < 3)
            {
                return new CommandResult { Ok = false, Error = "error: missing app id" };
            }
            command = new LaunchAppCommand(envName, args[2]);
            break;
        }

        case "enter":
        {
            string? dir = null;
            for (var i = 2; i < args.Length; i++)
            {
                if (args[i].Equals("--dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    dir = args[++i];
                }
            }
            command = new EnterCommand(envName, dir, newWindow: false);
            break;
        }

        default:
            return new CommandResult { Ok = false, Error = $"error: unknown env verb '{verb}'" };
    }

    return await dispatcher.RunAsync(command, Emit, ct);
}
