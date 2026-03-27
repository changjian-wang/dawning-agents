namespace Dawning.Agents.MCP.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// Represents an MCP resource definition.
/// </summary>
public sealed class MCPResource
{
    [JsonPropertyName("uri")]
    public required string Uri { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}

/// <summary>
/// Represents a resource template definition.
/// </summary>
public sealed class MCPResourceTemplate
{
    [JsonPropertyName("uriTemplate")]
    public required string UriTemplate { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}

/// <summary>
/// Represents the parameters for a list resources request.
/// </summary>
public sealed class ListResourcesParams
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// Represents the result of a list resources request.
/// </summary>
public sealed class ListResourcesResult
{
    [JsonPropertyName("resources")]
    public required List<MCPResource> Resources { get; init; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; init; }
}

/// <summary>
/// Represents the result of a list resource templates request.
/// </summary>
public sealed class ListResourceTemplatesResult
{
    [JsonPropertyName("resourceTemplates")]
    public required List<MCPResourceTemplate> ResourceTemplates { get; set; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

/// <summary>
/// Represents the parameters for a read resource request.
/// </summary>
public sealed class ReadResourceParams
{
    [JsonPropertyName("uri")]
    public required string Uri { get; set; }
}

/// <summary>
/// Represents the result of a read resource request.
/// </summary>
public sealed class ReadResourceResult
{
    [JsonPropertyName("contents")]
    public required List<ResourceContent> Contents { get; set; }
}

/// <summary>
/// Represents resource content.
/// </summary>
public sealed class ResourceContent
{
    [JsonPropertyName("uri")]
    public required string Uri { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("blob")]
    public string? Blob { get; set; }
}

/// <summary>
/// Represents the parameters for a resource subscribe request.
/// </summary>
public sealed class SubscribeResourceParams
{
    [JsonPropertyName("uri")]
    public required string Uri { get; set; }
}

/// <summary>
/// Represents the parameters for a resource unsubscribe request.
/// </summary>
public sealed class UnsubscribeResourceParams
{
    [JsonPropertyName("uri")]
    public required string Uri { get; set; }
}

/// <summary>
/// Represents the parameters for a resource updated notification.
/// </summary>
public sealed class ResourceUpdatedParams
{
    [JsonPropertyName("uri")]
    public required string Uri { get; set; }
}

/// <summary>
/// Represents the parameters for a resource list changed notification.
/// </summary>
public sealed class ResourceListChangedParams { }
