namespace QCompact.Tests;

/// <summary>Redirects Console.Out/Error for the duration of a test.</summary>
internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter _out;
    private readonly TextWriter _err;

    public StringWriter Out { get; } = new();
    public StringWriter Err { get; } = new();

    public ConsoleCapture()
    {
        _out = Console.Out;
        _err = Console.Error;
        Console.SetOut(Out);
        Console.SetError(Err);
    }

    public void Dispose()
    {
        Console.SetOut(_out);
        Console.SetError(_err);
        Out.Dispose();
        Err.Dispose();
    }
}

public class LoggerTests
{
    private static string WriteTranscript(Action<Logger> act)
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var log = new Logger(json: false, verbose: false, path))
            {
                act(log);
            }
            return File.ReadAllText(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InfoGoesToConsoleAndTranscript()
    {
        using var capture = new ConsoleCapture();
        var transcript = WriteTranscript(log => log.Info("hello"));

        Assert.Contains("hello", capture.Out.ToString());
        Assert.Contains("hello", transcript);
    }

    [Fact]
    public void JsonModeSuppressesConsoleInfoButStillTranscripts()
    {
        using var capture = new ConsoleCapture();
        var path = Path.GetTempFileName();
        try
        {
            using (var log = new Logger(json: true, verbose: false, path))
            {
                log.Info("hidden on console");
            }

            Assert.DoesNotContain("hidden on console", capture.Out.ToString());
            Assert.Contains("hidden on console", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerboseOnlyWritesWhenEnabled(bool verbose)
    {
        using var capture = new ConsoleCapture();
        using var log = new Logger(json: false, verbose, null);
        log.Verbose("detail");

        Assert.Equal(verbose, capture.Err.ToString().Contains("detail"));
    }

    [Fact]
    public void WarnAndErrorAlwaysReachConsoleEvenInJsonMode()
    {
        using var capture = new ConsoleCapture();
        using var log = new Logger(json: true, verbose: false, null);
        log.Warn("careful");
        log.Error("broken");

        var err = capture.Err.ToString();
        Assert.Contains("WARNING: careful", err);
        Assert.Contains("ERROR: broken", err);
    }
}