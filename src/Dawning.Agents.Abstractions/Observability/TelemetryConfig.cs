namespace Dawning.Agents.Abstractions.Observability;

/// <summary>
/// Configuration options for agent telemetry.
/// </summary>
/// <remarks>
/// Example appsettings.json:
/// <code>
/// {
///   "Telemetry": {
///     "ServiceName": "MyAgentApp",
///     "EnableLogging": true,
///     "EnableMetrics": true,
///     "EnableTracing": true,
///     "TraceSampleRate": 1.0
///   }
/// }
/// </code>
/// </remarks>
public class TelemetryConfig : IValidatableOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "Telemetry";

    /// <summary>
    /// Gets or sets a value indicating whether logging is enabled.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether metrics collection is enabled.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether distributed tracing is enabled.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets the telemetry service name.
    /// </summary>
    public string ServiceName { get; set; } = "Dawning.Agents";

    /// <summary>
    /// Gets or sets the service version.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the environment (dev, staging, prod).
    /// </summary>
    public string Environment { get; set; } = "development";

    /// <summary>
    /// Gets or sets the minimum log level.
    /// </summary>
    public TelemetryLogLevel MinLogLevel { get; set; } = TelemetryLogLevel.Information;

    /// <summary>
    /// Gets or sets the trace sampling rate (0.0 - 1.0).
    /// </summary>
    public double TraceSampleRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the optional OTLP endpoint.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            throw new InvalidOperationException("ServiceName is required.");
        }

        if (TraceSampleRate < 0.0 || TraceSampleRate > 1.0)
        {
            throw new InvalidOperationException("TraceSampleRate must be between 0.0 and 1.0.");
        }
    }
}

/// <summary>
/// Defines log severity levels for telemetry.
/// </summary>
public enum TelemetryLogLevel
{
    /// <summary>
    /// Trace level.
    /// </summary>
    Trace = 0,

    /// <summary>
    /// Debug level.
    /// </summary>
    Debug = 1,

    /// <summary>
    /// Information level.
    /// </summary>
    Information = 2,

    /// <summary>
    /// Warning level.
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Error level.
    /// </summary>
    Error = 4,

    /// <summary>
    /// Critical level.
    /// </summary>
    Critical = 5,
}
