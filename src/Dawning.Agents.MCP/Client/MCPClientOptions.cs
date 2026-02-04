namespace Dawning.Agents.MCP.Client;

/// <summary>
/// MCP Client 配置选项
/// </summary>
public sealed class MCPClientOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "MCPClient";

    /// <summary>
    /// 客户端名称
    /// </summary>
    public string Name { get; set; } = "Dawning.Agents";

    /// <summary>
    /// 客户端版本
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 工具调用超时时间（秒）
    /// </summary>
    public int ToolCallTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// 是否自动重连
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// 最大重连次数
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// 重连间隔（秒）
    /// </summary>
    public int ReconnectIntervalSeconds { get; set; } = 5;
}
