namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// Tool definition declared to the LLM for native function calling.
/// </summary>
public record ToolDefinition
{
    /// <summary>Tool name (corresponds to the function name).</summary>
    public required string Name { get; init; }

    /// <summary>Tool description (helps the LLM understand when to use it).</summary>
    public required string Description { get; init; }

    /// <summary>JSON Schema for parameters (conforming to the OpenAI function calling specification).</summary>
    public string? ParametersSchema { get; init; }

    /// <summary>
    /// Creates a tool definition from an ITool.
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
