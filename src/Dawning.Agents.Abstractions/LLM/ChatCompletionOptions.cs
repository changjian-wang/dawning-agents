namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// Options for a chat completion request.
/// </summary>
public record ChatCompletionOptions
{
    /// <summary>Sampling temperature (0.0–2.0). Higher values produce more random output.</summary>
    public float Temperature { get; init; } = 0.7f;

    /// <summary>Maximum number of tokens to generate.</summary>
    public int MaxTokens { get; init; } = 1000;

    /// <summary>System prompt.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Available tool definitions for native function calling.</summary>
    public IReadOnlyList<ToolDefinition>? Tools { get; init; }

    /// <summary>Tool choice mode (Auto/None/Required).</summary>
    public ToolChoiceMode? ToolChoice { get; init; }

    /// <summary>Response format (Text/JsonObject/JsonSchema).</summary>
    public ResponseFormat? ResponseFormat { get; init; }
}
