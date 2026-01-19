namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 标记方法为可被 Agent 调用的工具
/// </summary>
/// <remarks>
/// <para>使用此特性标记的方法会被自动扫描并注册为工具</para>
/// <para>方法签名要求：</para>
/// <list type="bullet">
/// <item>返回类型：string, Task&lt;string&gt;, ToolResult, 或 Task&lt;ToolResult&gt;</item>
/// <item>参数：支持基本类型（string, int, bool, double 等）</item>
/// <item>可选：最后一个参数可以是 CancellationToken</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// [FunctionTool("搜索网页内容")]
/// public string Search(string query) => $"Results for: {query}";
///
/// [FunctionTool("计算数学表达式")]
/// public async Task&lt;string&gt; CalculateAsync(string expression, CancellationToken ct = default)
/// {
///     // 实现
/// }
///
/// // 高风险操作需要确认
/// [FunctionTool("执行终端命令", RequiresConfirmation = true, RiskLevel = ToolRiskLevel.High)]
/// public string RunCommand(string command) { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FunctionToolAttribute : Attribute
{
    /// <summary>
    /// 工具描述（供 LLM 理解工具用途）
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 工具名称（可选，默认使用方法名）
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 是否需要用户确认才能执行（用于危险操作）
    /// </summary>
    /// <remarks>
    /// 当设置为 true 时，工具执行前会请求用户确认。
    /// 适用于：删除文件、执行命令、修改系统设置等操作。
    /// </remarks>
    public bool RequiresConfirmation { get; set; } = false;

    /// <summary>
    /// 工具的风险等级
    /// </summary>
    public ToolRiskLevel RiskLevel { get; set; } = ToolRiskLevel.Low;

    /// <summary>
    /// 工具分类（用于分组显示）
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 创建 FunctionTool 特性
    /// </summary>
    /// <param name="description">工具描述</param>
    public FunctionToolAttribute(string description)
    {
        Description = description;
    }
}

/// <summary>
/// 工具风险等级
/// </summary>
public enum ToolRiskLevel
{
    /// <summary>
    /// 低风险 - 只读操作，无副作用（如：获取时间、计算、格式化）
    /// </summary>
    Low = 0,

    /// <summary>
    /// 中等风险 - 可能有副作用但可恢复（如：写文件、HTTP 请求）
    /// </summary>
    Medium = 1,

    /// <summary>
    /// 高风险 - 可能造成不可逆影响（如：删除文件、执行命令、Git 操作）
    /// </summary>
    High = 2,
}

/// <summary>
/// 标记参数的描述信息（供 LLM 理解参数用途）
/// </summary>
/// <example>
/// <code>
/// [FunctionTool("搜索网页")]
/// public string Search(
///     [ToolParameter("搜索关键词")] string query,
///     [ToolParameter("返回结果数量")] int count = 10)
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ToolParameterAttribute : Attribute
{
    /// <summary>
    /// 参数描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 创建参数描述特性
    /// </summary>
    /// <param name="description">参数描述</param>
    public ToolParameterAttribute(string description)
    {
        Description = description;
    }
}
