namespace QCompact;

/// <summary>
/// Writes human-readable progress to the console (suppressed in JSON mode) and
/// tees every line to an optional transcript file.
/// </summary>
public sealed class Logger : IDisposable
{
    private readonly bool _json;
    private readonly bool _verbose;
    private readonly StreamWriter? _file;

    public Logger(bool json, bool verbose, string? logPath)
    {
        _json = json;
        _verbose = verbose;

        if (logPath is not null)
        {
            try
            {
                _file = new StreamWriter(logPath) { AutoFlush = true };
            }
            catch
            {
                // A transcript is a convenience; carry on without it.
                _file = null;
            }
        }
    }

    public void Info(string message) => Write(Console.Out, message, toConsole: !_json);

    public void Verbose(string message)
    {
        if (_verbose)
        {
            Write(Console.Error, message, toConsole: !_json);
        }
    }

    public void Warn(string message) => Write(Console.Error, "WARNING: " + message, toConsole: true);

    public void Error(string message) => Write(Console.Error, "ERROR: " + message, toConsole: true);

    public void Dispose() => _file?.Dispose();

    private void Write(TextWriter writer, string message, bool toConsole)
    {
        if (toConsole)
        {
            writer.WriteLine(message);
        }

        _file?.WriteLine(message);
    }
}
