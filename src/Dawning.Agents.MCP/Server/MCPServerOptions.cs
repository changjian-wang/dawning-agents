using Dawning.Agents.Abstractions;

namespace Dawning.Agents.MCP.Server;

/// <summary>
/// MCP Server 配置选项
/// </summary>
public sealed class MCPServerOptions : IValidatableOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "MCP";

    /// <summary>
    /// 服务器名称
    /// </summary>
    public string Name { get; set; } = "Dawning.Agents.MCP";

    /// <summary>
    /// 服务器版本
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 是否启用工具功能
    /// </summary>
    public bool EnableTools { get; set; } = true;

    /// <summary>
    /// 是否启用资源功能
    /// </summary>
    public bool EnableResources { get; set; } = true;

    /// <summary>
    /// 是否启用提示词功能
    /// </summary>
    public bool EnablePrompts { get; set; } = true;

    /// <summary>
    /// 是否启用日志功能
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// 工具执行超时时间（秒）
    /// </summary>
    public int ToolTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 最大并发请求数
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// 传输类型
    /// </summary>
    public MCPTransportType TransportType { get; set; } = MCPTransportType.Stdio;

    /// <summary>
    /// HTTP 端口（仅 HTTP 传输时使用）
    /// </summary>
    public int HttpPort { get; set; } = 8080;

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("MCP Server Name is required");
        }

        if (ToolTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("ToolTimeoutSeconds must be greater than 0");
        }

        if (MaxConcurrentRequests <= 0)
        {
            throw new InvalidOperationException("MaxConcurrentRequests must be greater than 0");
        }

        if (HttpPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("HttpPort must be between 1 and 65535");
        }
    }
}

/// <summary>
/// MCP 传输类型
/// </summary>
public enum MCPTransportType
{
    /// <summary>
    /// 标准输入/输出流（推荐，兼容 Claude Desktop）
    /// </summary>
    Stdio,

    /// <summary>
    /// HTTP + Server-Sent Events
    /// </summary>
    Http,
}
