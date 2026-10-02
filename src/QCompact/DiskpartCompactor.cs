using System.Diagnostics;
using System.Text;

namespace QCompact;

public static class DiskpartCompactor
{
    public static string BuildScript(string vhdxPath) =>
        string.Join("\r\n", new[]
        {
            $"select vdisk file=\"{vhdxPath}\"",
            "attach vdisk readonly",
            "compact vdisk",
            "detach vdisk",
            "exit",
        });

    public static void Compact(string vhdxPath, Action<string>? log = null)
    {
        var scriptFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(scriptFile, BuildScript(vhdxPath), Encoding.ASCII);

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

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"diskpart exited with code {process.ExitCode}{Environment.NewLine}{output}{error}");
            }

            if (log is not null)
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    log(line.TrimEnd());
                }
            }
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
