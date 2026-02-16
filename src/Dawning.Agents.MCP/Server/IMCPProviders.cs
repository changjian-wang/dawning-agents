namespace Dawning.Agents.MCP.Server;

using Dawning.Agents.MCP.Protocol;

/// <summary>
/// MCP 资源提供者接口
/// </summary>
public interface IMCPResourceProvider
{
    /// <summary>
    /// 获取所有可用资源
    /// </summary>
    IEnumerable<MCPResource> GetResources();

    /// <summary>
    /// 获取资源模板
    /// </summary>
    IEnumerable<MCPResourceTemplate> GetResourceTemplates() => [];

    /// <summary>
    /// 读取资源内容
    /// </summary>
    Task<ResourceContent?> ReadResourceAsync(
        string uri,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 是否支持指定 URI
    /// </summary>
    bool SupportsUri(string uri);
}

/// <summary>
/// MCP 提示词提供者接口
/// </summary>
public interface IMCPPromptProvider
{
    /// <summary>
    /// 获取所有可用提示词
    /// </summary>
    IEnumerable<MCPPrompt> GetPrompts();

    /// <summary>
    /// 获取提示词内容
    /// </summary>
    Task<GetPromptResult?> GetPromptAsync(
        string name,
        Dictionary<string, string>? arguments,
        CancellationToken cancellationToken = default
    );
}
