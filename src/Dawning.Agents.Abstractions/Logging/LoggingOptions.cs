using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Logging;

/// <summary>
/// Configuration options for agent logging.
/// </summary>
/// <remarks>
/// Example appsettings.json configuration:
/// <code>
/// {
///   "AgentLogging": {
///     "MinimumLevel": "Information",
///     "EnableConsole": true,
///     "EnableFile": true,
///     "FilePath": "logs/agent-.log",
///     "RollingInterval": "Day",
///     "RetainedFileCount": 30,
///     "OutputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
///     "EnableJsonFormat": false,
///     "EnrichWithMachineName": true,
///     "EnrichWithThreadId": true
///   }
/// }
/// </code>
/// </remarks>
public class LoggingOptions : IValidatableOptions
{
    private static readonly string[] s_validLevels =
    [
        "Verbose",
        "Debug",
        "Information",
        "Warning",
        "Error",
        "Fatal",
    ];

    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "AgentLogging";

    /// <summary>
    /// The minimum log level.
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Gets or sets a value indicating whether console output is enabled.
    /// </summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether file output is enabled.
    /// </summary>
    public bool EnableFile { get; set; } = false;

    /// <summary>
    /// The log file path (supports rolling placeholders).
    /// </summary>
    public string FilePath { get; set; } = "logs/agent-.log";

    /// <summary>
    /// The file rolling interval.
    /// </summary>
    public RollingIntervalType RollingInterval { get; set; } = RollingIntervalType.Day;

    /// <summary>
    /// The number of retained log files.
    /// </summary>
    public int RetainedFileCount { get; set; } = 30;

    /// <summary>
    /// The output template.
    /// </summary>
    public string OutputTemplate { get; set; } =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{AgentName}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Gets or sets a value indicating whether to use JSON format (suitable for ELK/Seq).
    /// </summary>
    public bool EnableJsonFormat { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enrich with machine name.
    /// </summary>
    public bool EnrichWithMachineName { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enrich with thread ID.
    /// </summary>
    public bool EnrichWithThreadId { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enrich with request ID.
    /// </summary>
    public bool EnrichWithRequestId { get; set; } = true;

    /// <summary>
    /// The log level overrides for specific namespaces.
    /// </summary>
    public IDictionary<string, string> Override { get; set; } =
        new Dictionary<string, string> { ["Microsoft"] = "Warning", ["System"] = "Warning" };

    /// <summary>
    /// The Elasticsearch logging options.
    /// </summary>
    public ElasticsearchLoggingOptions? Elasticsearch { get; set; }

    /// <summary>
    /// The Seq logging options (recommended for development).
    /// </summary>
    public SeqLoggingOptions? Seq { get; set; }

    /// <inheritdoc />
    public void Validate()
    {
        if (!s_validLevels.Contains(MinimumLevel, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"MinimumLevel '{MinimumLevel}' is not valid. Must be one of: {string.Join(", ", s_validLevels)}"
            );
        }

        if (RetainedFileCount <= 0)
        {
            throw new InvalidOperationException("RetainedFileCount must be greater than 0");
        }

        if (EnableFile && string.IsNullOrWhiteSpace(FilePath))
        {
            throw new InvalidOperationException(
                "FilePath is required when file logging is enabled"
            );
        }

        if (string.IsNullOrWhiteSpace(OutputTemplate))
        {
            throw new InvalidOperationException("OutputTemplate is required");
        }

        Elasticsearch?.Validate();
        Seq?.Validate();
    }
}

/// <summary>
/// Configuration options for Elasticsearch logging.
/// </summary>
public class ElasticsearchLoggingOptions : IValidatableOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Elasticsearch logging is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The Elasticsearch node URIs.
    /// </summary>
    public string[] NodeUris { get; set; } = ["http://localhost:9200"];

    /// <summary>
    /// The index format (supports date placeholders).
    /// </summary>
    public string IndexFormat { get; set; } = "dawning-agents-{0:yyyy.MM.dd}";

    /// <summary>
    /// Gets or sets a value indicating whether to auto-register index templates.
    /// </summary>
    public bool AutoRegisterTemplate { get; set; } = true;

    /// <summary>
    /// The API key (optional).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The username (optional, for Basic Auth).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// The password (optional, for Basic Auth).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// The batch size.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// The batch interval in seconds.
    /// </summary>
    public int BatchIntervalSeconds { get; set; } = 2;

    /// <inheritdoc />
    public void Validate()
    {
        if (Enabled && (NodeUris == null || NodeUris.Length == 0))
        {
            throw new InvalidOperationException(
                "Elasticsearch NodeUris must contain at least one URI when enabled"
            );
        }

        if (string.IsNullOrWhiteSpace(IndexFormat))
        {
            throw new InvalidOperationException("Elasticsearch IndexFormat is required");
        }

        if (BatchSize <= 0)
        {
            throw new InvalidOperationException("Elasticsearch BatchSize must be greater than 0");
        }

        if (BatchIntervalSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Elasticsearch BatchIntervalSeconds must be greater than 0"
            );
        }
    }
}

/// <summary>
/// Configuration options for Seq logging.
/// </summary>
public class SeqLoggingOptions : IValidatableOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Seq logging is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The Seq server URL.
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:5341";

    /// <summary>
    /// The API key (optional).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The batch interval in seconds.
    /// </summary>
    public int BatchIntervalSeconds { get; set; } = 2;

    /// <inheritdoc />
    public void Validate()
    {
        if (Enabled && string.IsNullOrWhiteSpace(ServerUrl))
        {
            throw new InvalidOperationException("Seq ServerUrl is required when enabled");
        }

        if (BatchIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("Seq BatchIntervalSeconds must be greater than 0");
        }
    }
}

/// <summary>
/// Specifies the log file rolling interval type.
/// </summary>
public enum RollingIntervalType
{
    /// <summary>
    /// No rolling (infinite).
    /// </summary>
    Infinite,

    /// <summary>
    /// Roll by year.
    /// </summary>
    Year,

    /// <summary>
    /// Roll by month.
    /// </summary>
    Month,

    /// <summary>
    /// Roll by day.
    /// </summary>
    Day,

    /// <summary>
    /// Roll by hour.
    /// </summary>
    Hour,

    /// <summary>
    /// Roll by minute.
    /// </summary>
    Minute,
}
