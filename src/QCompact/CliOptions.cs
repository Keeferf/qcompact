namespace QCompact;

public sealed class CliOptions
{
    public List<string> Distros { get; } = new();

    public bool DryRun { get; private set; }

    public bool Elevate { get; private set; } = true;

    public bool Yes { get; private set; }

    public bool Json { get; private set; }

    public bool KeepLog { get; private set; }

    public bool Verbose { get; private set; }

    public bool Help { get; private set; }

    public bool Version { get; private set; }

    public bool SelfUpdate { get; private set; }

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    options.Help = true;
                    break;
                case "-v":
                case "--version":
                    options.Version = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "--elevate":
                    options.Elevate = true;
                    break;
                case "--no-elevate":
                    options.Elevate = false;
                    break;
                case "-y":
                case "--yes":
                    options.Yes = true;
                    break;
                case "--json":
                    options.Json = true;
                    break;
                case "--keep-log":
                    options.KeepLog = true;
                    break;
                case "--verbose":
                    options.Verbose = true;
                    break;
                case "self-update":
                    options.SelfUpdate = true;
                    break;
                case "-d":
                case "--distro":
                    if (i + 1 >= args.Length)
                    {
                        throw new CliUsageException($"Option {arg} requires a value.");
                    }

                    options.Distros.Add(args[++i]);
                    break;
                default:
                    if (arg.StartsWith("--distro=", StringComparison.Ordinal))
                    {
                        options.Distros.Add(arg["--distro=".Length..]);
                    }
                    else
                    {
                        throw new CliUsageException($"Unknown option: {arg}");
                    }

                    break;
            }
        }

        return options;
    }
}

public sealed class CliUsageException : Exception
{
    public CliUsageException(string message)
        : base(message)
    {
    }
}
