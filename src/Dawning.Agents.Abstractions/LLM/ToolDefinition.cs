namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// 向 LLM 声明的工具定义（用于 Native Function Calling）
/// </summary>
public record ToolDefinition
{
    /// <summary>工具名称（对应函数名）</summary>
    public required string Name { get; init; }

    /// <summary>工具描述（帮助 LLM 理解何时使用）</summary>
    public required string Description { get; init; }

    /// <summary>参数的 JSON Schema（符合 OpenAI function calling 规范）</summary>
    public string? ParametersSchema { get; init; }

    /// <summary>
    /// 从 ITool 创建工具定义
    /// </summary>
    public static ToolDefinition FromTool(
        string name,
        string description,
        string? parametersSchema = null
    ) =>
        new()
        {
            Name = name,
            Description = description,
            ParametersSchema = parametersSchema,
        };
}
