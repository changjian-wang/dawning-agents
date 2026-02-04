namespace Dawning.Agents.MCP.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// MCP 资源定义
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
/// 资源模板定义
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
/// 资源列表请求参数
/// </summary>
public sealed class ListResourcesParams
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// 资源列表响应
/// </summary>
public sealed class ListResourcesResult
{
    [JsonPropertyName("resources")]
    public required List<MCPResource> Resources { get; set; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

/// <summary>
/// 资源模板列表响应
/// </summary>
public sealed class ListResourceTemplatesResult
{
    [JsonPropertyName("resourceTemplates")]
    public required List<MCPResourceTemplate> ResourceTemplates { get; set; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

/// <summary>
/// 读取资源请求参数
/// </summary>
public sealed class ReadResourceParams
{
    [JsonPropertyName("uri")]
    public required string Uri { get; set; }
}

/// <summary>
/// 读取资源响应
/// </summary>
public sealed class ReadResourceResult
{
    [JsonPropertyName("contents")]
    public required List<ResourceContent> Contents { get; set; }
}

/// <summary>
/// 资源内容
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
/// 资源订阅请求参数
/// </summary>
public sealed class SubscribeResourceParams
{
    [JsonPropertyName("uri")]
    public required string Uri { get; set; }
}

/// <summary>
/// 资源取消订阅请求参数
/// </summary>
public sealed class UnsubscribeResourceParams
{
    [JsonPropertyName("uri")]
    public required string Uri { get; set; }
}

/// <summary>
/// 资源更新通知参数
/// </summary>
public sealed class ResourceUpdatedParams
{
    [JsonPropertyName("uri")]
    public required string Uri { get; set; }
}

/// <summary>
/// 资源列表变更通知参数
/// </summary>
public sealed class ResourceListChangedParams { }
