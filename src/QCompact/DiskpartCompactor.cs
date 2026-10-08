using System.Diagnostics;
using System.Text;

namespace QCompact;

public static class DiskpartCompactor
{
    // diskpart's exit code is not reliable and its messages are localized, so we
    // treat an exit of 0 as success unless a known failure marker appears.
    // ponytail: English-only markers; if non-English Windows report false
    // successes, add localized markers or switch to Optimize-VHD.
    private static readonly string[] ErrorMarkers =
    {
        "Virtual Disk Service error",
        "error code",
        "system cannot find",
        "is not found",
        "failed",
        "access is denied",
    };

    // Large VHDX compactions are slow; only kill diskpart if it truly hangs.
    // ponytail: fixed ceiling; expose a --timeout flag if a real disk exceeds it.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(60);

    public static string BuildScript(string vhdxPath) =>
        string.Join("\r\n", new[]
        {
            $"select vdisk file=\"{vhdxPath}\"",
            "attach vdisk readonly",
            "compact vdisk",
            "detach vdisk",
            "exit",
        });

    public static string BuildDetachScript(string vhdxPath) =>
        string.Join("\r\n", new[]
        {
            $"select vdisk file=\"{vhdxPath}\"",
            "detach vdisk",
            "exit",
        });

    public static bool IsSuccess(int exitCode, string output, string error)
    {
        if (exitCode != 0)
        {
            return false;
        }

        var text = output + "\n" + error;
        return !ErrorMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Closes any diskpart.exe left running from a previous session. A stale
    /// instance can keep a VHDX attached, which blocks both WSL startup and
    /// compaction.
    /// </summary>
    public static void CloseExistingDiskpart(Action<string>? log = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var process in Process.GetProcessesByName("diskpart"))
        {
            using (process)
            {
                try
                {
                    var pid = process.Id;
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                    log?.Invoke($"Closed a running diskpart.exe (pid {pid}).");
                }
                catch
                {
                    // Best effort; the compaction itself reports real failures.
                }
            }
        }
    }

    public static void Compact(string vhdxPath, Action<string>? log = null)
    {
        var (exitCode, output, error) = RunScript(BuildScript(vhdxPath));

        if (!IsSuccess(exitCode, output, error))
        {
            // A failed compaction may have left the VHDX attached; try to release it.
            try
            {
                RunScript(BuildDetachScript(vhdxPath));
            }
            catch
            {
                // Best effort; the reported failure below is what matters.
            }

            throw new InvalidOperationException(
                $"diskpart failed to compact {vhdxPath} (exit {exitCode})." +
                $"{Environment.NewLine}{output}{error}");
        }

        if (log is not null)
        {
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                log(line.TrimEnd());
            }
        }
    }

    private static (int ExitCode, string Output, string Error) RunScript(string script)
    {
        var scriptFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(scriptFile, script, Encoding.ASCII);

            var startInfo = new ProcessStartInfo("diskpart.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("/s");
            startInfo.ArgumentList.Add(scriptFile);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start diskpart.exe.");

            // Read both streams concurrently; reading them serially can deadlock.
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(Timeout))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Already gone.
                }

                process.WaitForExit();
                throw new InvalidOperationException(
                    $"diskpart did not finish within {Timeout.TotalMinutes:N0} minutes and was terminated.");
            }

            Task.WaitAll(outputTask, errorTask);
            return (process.ExitCode, outputTask.Result, errorTask.Result);
        }
        finally
        {
            try
            {
                File.Delete(scriptFile);
            }
            catch
            {
                // Best effort cleanup of the temp diskpart script.
            }
        }
    }
}
