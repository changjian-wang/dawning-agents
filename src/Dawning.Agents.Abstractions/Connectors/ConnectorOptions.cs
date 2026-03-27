namespace Dawning.Agents.Abstractions.Connectors;

/// <summary>
/// Configuration options for connector authentication.
/// </summary>
public class ConnectorOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Connectors";

    /// <summary>
    /// API key (for API key-based authentication).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// OAuth2 client ID.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// OAuth2 client secret.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// OAuth2 tenant ID (for Microsoft Graph).
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Base endpoint URL.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <inheritdoc />
    public void Validate()
    {
        // At least one credential must be provided
        var hasApiKey = !string.IsNullOrWhiteSpace(ApiKey);
        var hasOAuth =
            !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

        if (!hasApiKey && !hasOAuth)
        {
            throw new InvalidOperationException(
                "Connector requires either ApiKey or ClientId + ClientSecret to be configured."
            );
        }
    }
}
