namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 工具执行沙箱 — 控制 bash/脚本命令的执行环境和安全策略
/// </summary>
public interface IToolSandbox
{
    /// <summary>
    /// 在沙箱中执行命令
    /// </summary>
    /// <param name="command">要执行的命令</param>
    /// <param name="options">沙箱选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<ToolExecutionResult> ExecuteAsync(
        string command,
        ToolSandboxOptions? options = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 沙箱执行选项
/// </summary>
public class ToolSandboxOptions
{
    /// <summary>
    /// 工作目录
    /// </summary>
    public string WorkingDirectory { get; set; } = ".";

    /// <summary>
    /// 最大执行时间
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 沙箱模式
    /// </summary>
    public SandboxMode Mode { get; set; } = SandboxMode.Trust;

    /// <summary>
    /// 环境变量
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = [];
}

/// <summary>
/// 沙箱模式
/// </summary>
public enum SandboxMode
{
    /// <summary>
    /// 信任模式 — 直接在主机执行（开发环境）
    /// </summary>
    Trust,

    /// <summary>
    /// 工作目录限制 — 限制在指定目录内执行
    /// </summary>
    WorkingDir,

    /// <summary>
    /// 超时模式 — 仅限制执行时间
    /// </summary>
    Timeout,
}

/// <summary>
/// 工具执行结果
/// </summary>
public record ToolExecutionResult
{
    /// <summary>
    /// 进程退出码
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// 标准输出
    /// </summary>
    public string Stdout { get; init; } = "";

    /// <summary>
    /// 标准错误
    /// </summary>
    public string Stderr { get; init; } = "";

    /// <summary>
    /// 执行耗时
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// 是否超时
    /// </summary>
    public bool TimedOut { get; init; }

    /// <summary>
    /// 是否执行成功（退出码为 0 且未超时）
    /// </summary>
    public bool IsSuccess => ExitCode == 0 && !TimedOut;
}
