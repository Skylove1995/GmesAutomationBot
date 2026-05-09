namespace GmesImporter.App;

public sealed class CommandLine
{
    private readonly Dictionary<string, string?> _options;

    private CommandLine(string name, Dictionary<string, string?> options)
    {
        Name = name;
        _options = options;
    }

    public string Name { get; }

    public static string HelpText =>
        """
        GMES Production Importer

        Commands:
          run-loop [--dry-run]                  Run continuously in a loop based on RunIntervalSeconds.
          run-once [--dry-run] [--file <xlsx>]   Export from GMES and import one cycle. Use --file to import an existing Excel file.
          dry-run [--file <xlsx>]               Run the same cycle without writing to database.
          validate-config                       Validate config, folders, driver, Edge path, and database connection.
          diagnose-db                           Show parsed DB server/port/user and test TCP/MySQL connectivity.
          protect-text <value>                  Encrypt a secret with Windows DPAPI for appsettings.json.
          help                                  Show this help.
        """;

    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return new CommandLine("help", new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
        }

        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var command = args[0];

        if (command == "protect-text" && args.Count > 1)
        {
            options["value"] = string.Join(' ', args.Skip(1));
            return new CommandLine(command, options);
        }

        for (var index = 1; index < args.Count; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var name = token[2..];
            if (index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[name] = args[++index];
            }
            else
            {
                options[name] = null;
            }
        }

        return new CommandLine(command, options);
    }

    public bool HasFlag(string name)
    {
        return _options.ContainsKey(name);
    }

    public string? GetOptionalValue(string name)
    {
        return _options.TryGetValue(name, out var value) ? value : null;
    }

    public string GetRequiredValue(string name)
    {
        return GetOptionalValue(name) ?? throw new ArgumentException($"Missing required argument '{name}'.");
    }
}
