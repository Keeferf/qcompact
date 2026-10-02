using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace QCompact;

public static class Program
{
    public static int Main(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (CliUsageException ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(HelpText);
            return 2;
        }

        if (options.Help)
        {
            Console.Out.WriteLine(HelpText);
            return 0;
        }

        if (options.Version)
        {
            Console.Out.WriteLine($"qcompact {CurrentVersion}");
            return 0;
        }

        var logPath = Path.Combine(Path.GetTempPath(), $"qcompact-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        using var log = new Logger(options.Json, options.Verbose, logPath);

        try
        {
            return Run(options, args, log);
        }
        finally
        {
            if (options.KeepLog)
            {
                Console.Out.WriteLine($"Log saved: {logPath}");
            }
            else
            {
                try
                {
                    File.Delete(logPath);
                }
                catch
                {
                    // Best effort.
                }
            }
        }
    }

    private static int Run(CliOptions options, string[] args, Logger log)
    {
        var targets = WslDiskLocator.Find()
            .Where(disk => disk.Exists)
            .Where(disk => options.Distros.Count == 0
                || options.Distros.Contains(disk.Distro, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (targets.Count == 0)
        {
            log.Warn("No WSL 2 virtual disks found. Is WSL installed, and are the distros in their default location?");
            return 0;
        }

        if (!options.Json)
        {
            log.Info("WSL 2 virtual disks detected:");
            foreach (var target in targets)
            {
                log.Info($"  {target.Distro,-24} {SizeFormatter.Format(SafeLength(target.Vhdx)),10}  {target.Vhdx}");
            }
        }

        if (options.DryRun)
        {
            if (options.Json)
            {
                WriteJson(options, targets, results: null);
            }
            else
            {
                log.Info($"{Environment.NewLine}Dry run: nothing was changed.");
            }

            return 0;
        }

        if (!Elevation.IsAdmin())
        {
            if (!options.Elevate)
            {
                log.Error(
                    "Administrator rights are required to compact virtual disks. " +
                    "Re-run from an elevated terminal, or pass --elevate.");
                return 2;
            }

            log.Info($"{Environment.NewLine}Elevation is required. Relaunching...");
            return Elevation.RelaunchElevated(args);
        }

        if (!options.Yes)
        {
            if (Console.IsInputRedirected)
            {
                log.Error("Refusing to compact without confirmation. Pass --yes for non-interactive use.");
                return 2;
            }

            Console.Out.Write($"Compact {targets.Count} disk(s)? [y/N] ");
            var answer = Console.ReadLine();
            if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
            {
                log.Info("Aborted.");
                return 0;
            }
        }

        log.Info($"{Environment.NewLine}Shutting down WSL...");
        ShutdownWsl();

        var results = new List<DiskResult>();
        var exitCode = 0;

        foreach (var target in targets)
        {
            var before = SafeLength(target.Vhdx);
            log.Info($"Compacting {target.Distro}...");
            try
            {
                DiskpartCompactor.Compact(target.Vhdx, log.Verbose);
            }
            catch (Exception ex)
            {
                log.Error($"Compaction failed for {target.Distro}: {ex.Message}");
                results.Add(new DiskResult(target.Distro, target.Vhdx, before, before, 0, "failed"));
                exitCode = 1;
                continue;
            }

            var after = SafeLength(target.Vhdx);
            var reclaimed = before - after;
            results.Add(new DiskResult(target.Distro, target.Vhdx, before, after, reclaimed, "compacted"));
            log.Info(
                $"  {target.Distro}: {SizeFormatter.Format(before)} -> {SizeFormatter.Format(after)}" +
                $"  (reclaimed {SizeFormatter.Format(reclaimed)})");
        }

        if (options.Json)
        {
            WriteJson(options, targets, results);
        }
        else
        {
            var total = results.Sum(result => result.ReclaimedBytes);
            log.Info($"{Environment.NewLine}Total reclaimed: {SizeFormatter.Format(total)}");
            log.Info("Start your WSL distribution again to continue working.");
        }

        return exitCode;
    }

    private static void ShutdownWsl()
    {
        try
        {
            var startInfo = new ProcessStartInfo("wsl.exe", "--shutdown")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(startInfo);
            process?.WaitForExit();
        }
        catch
        {
            // WSL may not be running; the compaction below still works.
        }

        Thread.Sleep(TimeSpan.FromSeconds(2));
    }

    private static long SafeLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static void WriteJson(CliOptions options, IReadOnlyList<WslDisk> targets, IReadOnlyList<DiskResult>? results)
    {
        var disks = results
            ?? targets
                .Select(target =>
                {
                    var size = SafeLength(target.Vhdx);
                    return new DiskResult(target.Distro, target.Vhdx, size, size, 0, "dry-run");
                })
                .ToList();

        var payload = new
        {
            command = "qcompact",
            version = CurrentVersion,
            dryRun = options.DryRun,
            disks,
            totalReclaimedBytes = disks.Sum(disk => disk.ReclaimedBytes),
        };

        Console.Out.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static string CurrentVersion =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(Program).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public const string HelpText = """
        qcompact - reclaim unused disk space from WSL 2 virtual disks.

        Usage:
          qcompact [options]

        Options:
          -d, --distro <name>   Distribution(s) to compact. Repeatable. Default: all.
              --dry-run         List detected disks and sizes; change nothing.
              --elevate         Relaunch elevated (UAC) if not already admin.
          -y, --yes             Do not prompt for confirmation.
              --json            Emit a machine-readable JSON result.
              --keep-log        Keep the transcript log file.
              --verbose         Verbose output.
          -v, --version         Show version information.
          -h, --help            Show this help.

        Exit codes:
          0  success
          1  one or more compactions failed
          2  usage, permission, or confirmation error
        """;
}

public sealed record DiskResult(string Distro, string Vhdx, long BeforeBytes, long AfterBytes, long ReclaimedBytes, string Status);
