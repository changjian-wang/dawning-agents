using Dawning.Agents.Abstractions;

namespace Dawning.Agents.MCP.Client;

/// <summary>
/// Configuration options for the MCP Client.
/// </summary>
public sealed class MCPClientOptions : IValidatableOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "MCPClient";

    /// <summary>
    /// Gets or sets the client name.
    /// </summary>
    public string Name { get; set; } = "Dawning.Agents";

    /// <summary>
    /// Gets or sets the client version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the connection timeout in seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the tool call timeout in seconds.
    /// </summary>
    public int ToolCallTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets a value indicating whether auto-reconnect is enabled.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of reconnect attempts.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the reconnect interval in seconds.
    /// </summary>
    public int ReconnectIntervalSeconds { get; set; } = 5;

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("MCPClient Name is required");
        }

        if (ConnectionTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("ConnectionTimeoutSeconds must be greater than 0");
        }

        if (RequestTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("RequestTimeoutSeconds must be greater than 0");
        }

        if (ToolCallTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("ToolCallTimeoutSeconds must be greater than 0");
        }

        if (MaxReconnectAttempts < 0)
        {
            throw new InvalidOperationException("MaxReconnectAttempts must be non-negative");
        }

        if (ReconnectIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("ReconnectIntervalSeconds must be greater than 0");
        }
    }
}
