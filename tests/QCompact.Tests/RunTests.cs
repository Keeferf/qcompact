using System.Text.Json;

namespace QCompact.Tests;

public class RunTests
{
    private static readonly WslDisk Ubuntu = new("Ubuntu", @"C:\wsl\Ubuntu", @"C:\wsl\Ubuntu\ext4.vhdx", Exists: true);
    private static readonly WslDisk Debian = new("Debian", @"C:\wsl\Debian", @"C:\wsl\Debian\ext4.vhdx", Exists: true);

    private static int Run(CliOptions options, Func<IReadOnlyList<WslDisk>> findDisks, Action<string, Action<string>?>? compact = null, Func<bool>? isAdmin = null)
    {
        using var log = new Logger(options.Json, options.Verbose, null);
        return Program.Run(
            options,
            Array.Empty<string>(),
            log,
            findDisks: findDisks,
            isAdmin: isAdmin ?? (() => true),
            shutdownWsl: () => { },
            compact: compact ?? ((path, _) => { }));
    }

    private static JsonDocument CaptureJson(CliOptions options, Func<IReadOnlyList<WslDisk>> findDisks, Action<string, Action<string>?>? compact = null)
    {
        using var capture = new ConsoleCapture();
        Assert.Equal(0, Run(options, findDisks, compact));
        return JsonDocument.Parse(capture.Out.ToString());
    }

    [Fact]
    public void SelectTargetsFiltersByDistroCaseInsensitiveAndSkipsMissing()
    {
        var options = CliOptions.Parse(new[] { "--distro", "ubuntu" });
        var disks = new[]
        {
            Ubuntu,
            Debian,
            new WslDisk("Ubuntu", "b3", "v3", Exists: false),
        };

        var targets = Program.SelectTargets(options, disks);

        var target = Assert.Single(targets);
        Assert.Equal("Ubuntu", target.Distro);
    }

    [Fact]
    public void SelectTargetsDefaultsToAllExistingDisks()
    {
        var targets = Program.SelectTargets(CliOptions.Parse(Array.Empty<string>()), new[] { Ubuntu, Debian });

        Assert.Equal(new[] { "Ubuntu", "Debian" }, targets.Select(t => t.Distro));
    }

    [Fact]
    public void NoDisksFoundReturnsZero()
    {
        using var capture = new ConsoleCapture();
        var code = Run(CliOptions.Parse(Array.Empty<string>()), () => Array.Empty<WslDisk>());

        Assert.Equal(0, code);
        Assert.Contains("WARNING: No WSL 2 virtual disks found", capture.Err.ToString());
    }

    [Fact]
    public void DryRunJsonReportsDisksWithoutCompacting()
    {
        using var json = CaptureJson(
            CliOptions.Parse(new[] { "--dry-run", "--json" }),
            () => new[] { Ubuntu, Debian });

        var root = json.RootElement;
        Assert.Equal("qcompact", root.GetProperty("command").GetString());
        Assert.True(root.GetProperty("dryRun").GetBoolean());
        Assert.Equal(0, root.GetProperty("totalReclaimedBytes").GetInt64());

        var disks = root.GetProperty("disks").EnumerateArray().ToList();
        Assert.Equal(2, disks.Count);
        Assert.Equal("Ubuntu", disks[0].GetProperty("distro").GetString());
        Assert.Equal("dry-run", disks[0].GetProperty("status").GetString());
    }

    [Fact]
    public void NonAdminNoElevateReturnsTwo()
    {
        var code = Run(
            CliOptions.Parse(new[] { "--no-elevate", "--yes" }),
            () => new[] { Ubuntu },
            isAdmin: () => false);

        Assert.Equal(2, code);
    }

    [Fact]
    public void NonAdminJsonReturnsTwoWithoutElevating()
    {
        var code = Run(
            CliOptions.Parse(new[] { "--json", "--yes" }),
            () => new[] { Ubuntu },
            isAdmin: () => false);

        Assert.Equal(2, code);
    }

    [Fact]
    public void FailedCompactionIsolatesAndOthersStillCompact()
    {
        using var capture = new ConsoleCapture();
        var options = CliOptions.Parse(new[] { "--json", "--yes" });
        var code = Run(
            options,
            () => new[] { Ubuntu, Debian },
            compact: (path, _) =>
            {
                if (path == Ubuntu.Vhdx)
                {
                    throw new InvalidOperationException("disk locked");
                }
            });

        Assert.Equal(1, code);

        using var json = JsonDocument.Parse(capture.Out.ToString());
        var disks = json.RootElement.GetProperty("disks").EnumerateArray().ToList();
        Assert.Equal("failed", disks.Single(d => d.GetProperty("distro").GetString() == "Ubuntu").GetProperty("status").GetString());
        Assert.Equal("compacted", disks.Single(d => d.GetProperty("distro").GetString() == "Debian").GetProperty("status").GetString());

        var failed = disks.Single(d => d.GetProperty("distro").GetString() == "Ubuntu");
        Assert.Equal(failed.GetProperty("beforeBytes").GetInt64(), failed.GetProperty("afterBytes").GetInt64());
    }

    [Fact]
    public void DeclinedConfirmationAbortsWithZero()
    {
        if (Console.IsInputRedirected)
        {
            return; // ponytail: stdin redirected, prompt branch unreachable in this runner.
        }

        var originalIn = Console.In;
        try
        {
            Console.SetIn(new StringReader("n\n"));
            var code = Run(
                CliOptions.Parse(Array.Empty<string>()),
                () => new[] { Ubuntu },
                isAdmin: () => true);

            Assert.Equal(0, code);
        }
        finally
        {
            Console.SetIn(originalIn);
        }
    }
}