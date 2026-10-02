using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace QCompact;

public static class Elevation
{
    public static bool IsAdmin()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static int RelaunchElevated(IReadOnlyList<string> args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath
                ?? throw new InvalidOperationException("Cannot determine the current executable path."),
            UseShellExecute = true,
            Verb = "runas",
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to relaunch elevated.");
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception)
        {
            // The user declined the UAC prompt.
            return 2;
        }
    }
}
