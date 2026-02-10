namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// LLM 响应格式配置
/// </summary>
public record ResponseFormat
{
    /// <summary>响应格式类型</summary>
    public ResponseFormatType Type { get; init; } = ResponseFormatType.Text;

    /// <summary>JSON Schema 名称（仅 JsonSchema 类型时使用）</summary>
    public string? SchemaName { get; init; }

    /// <summary>JSON Schema 定义（仅 JsonSchema 类型时使用）</summary>
    public string? Schema { get; init; }

    /// <summary>是否启用严格模式（仅 JsonSchema 类型时使用）</summary>
    public bool Strict { get; init; }

    /// <summary>普通文本格式</summary>
    public static readonly ResponseFormat Text = new() { Type = ResponseFormatType.Text };

    /// <summary>JSON 对象格式</summary>
    public static readonly ResponseFormat JsonObject =
        new() { Type = ResponseFormatType.JsonObject };

    /// <summary>
    /// 创建 JSON Schema 格式
    /// </summary>
    public static ResponseFormat JsonSchema(
        string schemaName,
        string schema,
        bool strict = true
    ) =>
        new()
        {
            Type = ResponseFormatType.JsonSchema,
            SchemaName = schemaName,
            Schema = schema,
            Strict = strict,
        };
}
