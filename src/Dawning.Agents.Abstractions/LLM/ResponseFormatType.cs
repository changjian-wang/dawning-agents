namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// LLM 响应格式类型
/// </summary>
public enum ResponseFormatType
{
    /// <summary>普通文本响应（默认）</summary>
    Text,

    /// <summary>强制返回合法 JSON 对象</summary>
    JsonObject,

    /// <summary>强制返回符合指定 JSON Schema 的对象</summary>
    JsonSchema,
}
