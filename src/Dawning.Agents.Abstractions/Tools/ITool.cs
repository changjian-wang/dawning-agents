namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 工具执行结果
/// </summary>
public record ToolResult
{
    /// <summary>
    /// 执行是否成功
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// 执行输出内容
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// 错误信息（当 Success = false 时）
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 是否需要用户确认（用于高风险操作的预检）
    /// </summary>
    public bool RequiresConfirmation { get; init; } = false;

    /// <summary>
    /// 确认提示消息
    /// </summary>
    public string? ConfirmationMessage { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ToolResult Ok(string output) => new() { Success = true, Output = output };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ToolResult Fail(string error) =>
        new()
        {
            Success = false,
            Output = string.Empty,
            Error = error,
        };

    /// <summary>
    /// 创建需要确认的结果
    /// </summary>
    public static ToolResult NeedConfirmation(string message) =>
        new()
        {
            Success = false,
            Output = string.Empty,
            RequiresConfirmation = true,
            ConfirmationMessage = message,
        };
}

/// <summary>
/// 工具接口 - Agent 可调用的外部能力
/// </summary>
/// <remarks>
/// <para>工具是 Agent 与外部世界交互的桥梁</para>
/// <para>每个工具有名称、描述和参数 Schema，供 LLM 理解如何调用</para>
/// </remarks>
public interface ITool
{
    /// <summary>
    /// 工具名称（唯一标识符）
    /// </summary>
    /// <example>Search, Calculate, GetWeather</example>
    string Name { get; }

    /// <summary>
    /// 工具描述（供 LLM 理解工具用途）
    /// </summary>
    /// <example>搜索网页内容并返回相关结果</example>
    string Description { get; }

    /// <summary>
    /// 参数的 JSON Schema（供 LLM 理解参数格式）
    /// </summary>
    /// <example>{"type":"object","properties":{"query":{"type":"string"}}}</example>
    string ParametersSchema { get; }

    /// <summary>
    /// 是否需要用户确认才能执行
    /// </summary>
    bool RequiresConfirmation { get; }

    /// <summary>
    /// 工具的风险等级
    /// </summary>
    ToolRiskLevel RiskLevel { get; }

    /// <summary>
    /// 工具分类
    /// </summary>
    string? Category { get; }

    /// <summary>
    /// 执行工具
    /// </summary>
    /// <param name="input">输入参数（字符串或 JSON）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<ToolResult> ExecuteAsync(string input, CancellationToken cancellationToken = default);
}
