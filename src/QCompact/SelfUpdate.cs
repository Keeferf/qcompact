using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace QCompact;

/// <summary>
/// Checks GitHub for a newer release and replaces the running executable in place.
/// </summary>
public static class SelfUpdate
{
    private const string Repo = "Keeferf/qcompact";
    private const string ExeName = "qcompact.exe";
    private const string ApiLatestUrl = $"https://api.github.com/repos/{Repo}/releases/latest";
    private const string LatestDownloadUrl = $"https://github.com/{Repo}/releases/latest/download";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"qcompact/{Program.CurrentVersion}");
        return client;
    }

    /// <summary>
    /// Installs the latest release over the running executable and relaunches.
    /// Returns the process exit code.
    /// </summary>
    public static int Run()
    {
        if (!TryGetLatestVersion(out var latest))
        {
            Console.Error.WriteLine("ERROR: Could not reach GitHub to check for updates.");
            return 2;
        }

        if (!IsNewer(Program.CurrentVersion, latest!))
        {
            Console.Out.WriteLine($"qcompact is already up to date (v{Program.CurrentVersion}).");
            return 0;
        }

        var target = Environment.ProcessPath;
        if (target is null)
        {
            Console.Error.WriteLine("ERROR: Could not determine where qcompact is installed.");
            return 2;
        }

        var tempExe = Path.Combine(Path.GetTempPath(), $"qcompact-{Guid.NewGuid():N}.exe");
        var tempSha = tempExe + ".sha256";

        try
        {
            Console.Out.WriteLine($"Downloading qcompact {latest}...");
            Download($"{LatestDownloadUrl}/{ExeName}", tempExe);
            Download($"{LatestDownloadUrl}/{ExeName}.sha256", tempSha);

            if (!MatchesChecksum(tempExe, tempSha))
            {
                Console.Error.WriteLine("ERROR: Checksum mismatch. The download was discarded.");
                return 2;
            }

            Console.Out.WriteLine("Checksum verified.");

            var batch = Path.Combine(Path.GetTempPath(), $"qcompact-update-{Guid.NewGuid():N}.cmd");
            File.WriteAllText(batch, BuildReplaceBatch(tempExe, tempSha, target).ReplaceLineEndings("\r\n"));

            Process.Start(new ProcessStartInfo(batch) { UseShellExecute = true });
            Console.Out.WriteLine("Update staged. Replacing the installed copy and relaunching...");
            return 0;
        }
        finally
        {
            // The temp exe must survive until the batch moves it; the batch cleans
            // up the checksum and itself. Anything left in %TEMP% is OS temp scope.
            try { File.Delete(tempSha); } catch { }
        }
    }

    /// <summary>
    /// Fetches the latest release tag (e.g. "v1.2.0"), or null when GitHub is
    /// unreachable or the response is unusable. Never throws.
    /// </summary>
    internal static bool TryGetLatestVersion(out string? latest)
    {
        latest = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var request = new HttpRequestMessage(HttpMethod.Get, ApiLatestUrl);
            using var response = Http.Send(request, cts.Token);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(response.Content.ReadAsStream(cts.Token));
            latest = doc.RootElement.GetProperty("tag_name").GetString();
            return latest is not null;
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsNewer(string current, string latest)
    {
        var currentVersion = ParseVersion(current);
        var latestVersion = ParseVersion(latest);
        return currentVersion is not null && latestVersion is not null && latestVersion > currentVersion;
    }

    internal static string Sha256OfFile(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    /// <summary>
    /// Returns true when the executable's SHA256 matches the first whitespace
    /// token in the published checksum file (the format install.ps1 relies on).
    /// </summary>
    internal static bool MatchesChecksum(string exePath, string shaFilePath)
    {
        if (!File.Exists(shaFilePath))
        {
            return false;
        }

        var parts = File
            .ReadAllText(shaFilePath)
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0
            && string.Equals(parts[0], Sha256OfFile(exePath), StringComparison.OrdinalIgnoreCase);
    }

    private static void Download(string url, string destination)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = Http.Send(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();

        using var file = File.Create(destination);
        response.Content.ReadAsStream(cts.Token).CopyTo(file);
    }

    /// <summary>
    /// Builds the batch file that swaps the new executable in once this process
    /// exits (a running exe is locked until then) and relaunches it.
    /// </summary>
    internal static string BuildReplaceBatch(string newExe, string shaFile, string target)
    {
        return $"""
            @echo off
            setlocal
            set "NEW={newExe}"
            set "SHA={shaFile}"
            set "TARGET={target}"
            set /a tries=0
            :retry
            timeout /t 1 /nobreak >nul
            move /y "%NEW%" "%TARGET%" >nul 2>&1
            if errorlevel 1 goto next
            goto run
            :next
            set /a tries+=1
            if %tries% geq 30 goto fail
            goto retry
            :run
            del "%SHA%" >nul 2>&1
            start "" "%TARGET%" --version
            (goto) 2>nul & del "%~f0" >nul 2>&1
            exit /b 0
            :fail
            echo Self-update failed: could not replace %TARGET%.
            echo The file may be locked or not writable. Run qcompact self-update from an elevated shell.
            pause
            exit /b 1
            """;
    }

    private static Version? ParseVersion(string value) =>
        Version.TryParse(value.TrimStart('v'), out var version) ? version : null;
}