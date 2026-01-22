namespace Dawning.Agents.Abstractions.Observability;

/// <summary>
/// Agent 遥测配置
/// </summary>
/// <remarks>
/// appsettings.json 示例:
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
public class TelemetryConfig
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Telemetry";

    /// <summary>
    /// 启用日志
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// 启用指标收集
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// 启用分布式追踪
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// 遥测服务名称
    /// </summary>
    public string ServiceName { get; set; } = "Dawning.Agents";

    /// <summary>
    /// 服务版本
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// 环境（dev, staging, prod）
    /// </summary>
    public string Environment { get; set; } = "development";

    /// <summary>
    /// 最低日志级别
    /// </summary>
    public TelemetryLogLevel MinLogLevel { get; set; } = TelemetryLogLevel.Information;

    /// <summary>
    /// 追踪采样率（0.0 - 1.0）
    /// </summary>
    public double TraceSampleRate { get; set; } = 1.0;

    /// <summary>
    /// OTLP 端点（可选）
    /// </summary>
    public string? OtlpEndpoint { get; set; }
}

/// <summary>
/// 日志级别
/// </summary>
public enum TelemetryLogLevel
{
    /// <summary>
    /// 跟踪
    /// </summary>
    Trace = 0,

    /// <summary>
    /// 调试
    /// </summary>
    Debug = 1,

    /// <summary>
    /// 信息
    /// </summary>
    Information = 2,

    /// <summary>
    /// 警告
    /// </summary>
    Warning = 3,

    /// <summary>
    /// 错误
    /// </summary>
    Error = 4,

    /// <summary>
    /// 严重
    /// </summary>
    Critical = 5,
}
