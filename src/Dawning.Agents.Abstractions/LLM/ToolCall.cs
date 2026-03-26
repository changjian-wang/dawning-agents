namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// Represents a function calling tool call returned by the LLM.
/// </summary>
/// <param name="Id">Unique identifier for the tool call (used to correlate tool message responses).</param>
/// <param name="FunctionName">Name of the function to invoke.</param>
/// <param name="Arguments">Serialized JSON arguments.</param>
public record ToolCall(string Id, string FunctionName, string Arguments);
