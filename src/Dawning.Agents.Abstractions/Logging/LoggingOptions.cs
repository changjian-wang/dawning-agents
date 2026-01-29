namespace Dawning.Agents.Abstractions.Logging;

/// <summary>
/// 日志配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
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
public class LoggingOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "AgentLogging";

    /// <summary>
    /// 最小日志级别
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// 是否启用控制台输出
    /// </summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>
    /// 是否启用文件输出
    /// </summary>
    public bool EnableFile { get; set; } = false;

    /// <summary>
    /// 日志文件路径（支持滚动占位符）
    /// </summary>
    public string FilePath { get; set; } = "logs/agent-.log";

    /// <summary>
    /// 文件滚动间隔
    /// </summary>
    public RollingIntervalType RollingInterval { get; set; } = RollingIntervalType.Day;

    /// <summary>
    /// 保留的文件数量
    /// </summary>
    public int RetainedFileCount { get; set; } = 30;

    /// <summary>
    /// 输出模板
    /// </summary>
    public string OutputTemplate { get; set; } =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{AgentName}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// 是否使用 JSON 格式（适合 ELK/Seq）
    /// </summary>
    public bool EnableJsonFormat { get; set; } = false;

    /// <summary>
    /// 是否添加机器名称
    /// </summary>
    public bool EnrichWithMachineName { get; set; } = true;

    /// <summary>
    /// 是否添加线程 ID
    /// </summary>
    public bool EnrichWithThreadId { get; set; } = true;

    /// <summary>
    /// 是否添加请求 ID
    /// </summary>
    public bool EnrichWithRequestId { get; set; } = true;

    /// <summary>
    /// 针对特定命名空间的日志级别覆盖
    /// </summary>
    public Dictionary<string, string> Override { get; set; } = new()
    {
        ["Microsoft"] = "Warning",
        ["System"] = "Warning",
    };

    /// <summary>
    /// Elasticsearch 配置
    /// </summary>
    public ElasticsearchLoggingOptions? Elasticsearch { get; set; }

    /// <summary>
    /// Seq 配置（开发环境推荐）
    /// </summary>
    public SeqLoggingOptions? Seq { get; set; }
}

/// <summary>
/// Elasticsearch 日志配置
/// </summary>
public class ElasticsearchLoggingOptions
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Elasticsearch 节点地址
    /// </summary>
    public string[] NodeUris { get; set; } = ["http://localhost:9200"];

    /// <summary>
    /// 索引格式（支持日期占位符）
    /// </summary>
    public string IndexFormat { get; set; } = "dawning-agents-{0:yyyy.MM.dd}";

    /// <summary>
    /// 自动注册索引模板
    /// </summary>
    public bool AutoRegisterTemplate { get; set; } = true;

    /// <summary>
    /// API Key（可选）
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 用户名（可选，Basic Auth）
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 密码（可选，Basic Auth）
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 批量发送大小
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// 批量发送间隔（秒）
    /// </summary>
    public int BatchIntervalSeconds { get; set; } = 2;
}

/// <summary>
/// Seq 日志配置
/// </summary>
public class SeqLoggingOptions
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Seq 服务器地址
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:5341";

    /// <summary>
    /// API Key（可选）
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 批量发送间隔（秒）
    /// </summary>
    public int BatchIntervalSeconds { get; set; } = 2;
}

/// <summary>
/// 日志文件滚动间隔类型
/// </summary>
public enum RollingIntervalType
{
    /// <summary>
    /// 无限制（不滚动）
    /// </summary>
    Infinite,

    /// <summary>
    /// 按年滚动
    /// </summary>
    Year,

    /// <summary>
    /// 按月滚动
    /// </summary>
    Month,

    /// <summary>
    /// 按天滚动
    /// </summary>
    Day,

    /// <summary>
    /// 按小时滚动
    /// </summary>
    Hour,

    /// <summary>
    /// 按分钟滚动
    /// </summary>
    Minute,
}
