namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// 表示 LLM 返回的 Function Calling 工具调用
/// </summary>
/// <param name="Id">工具调用唯一标识（用于关联 tool 消息的响应）</param>
/// <param name="FunctionName">要调用的函数名称</param>
/// <param name="Arguments">序列化的 JSON 参数</param>
public record ToolCall(string Id, string FunctionName, string Arguments);
