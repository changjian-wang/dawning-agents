namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// LLM response format type.
/// </summary>
public enum ResponseFormatType
{
    /// <summary>Plain text response (default).</summary>
    Text,

    /// <summary>Force a valid JSON object response.</summary>
    JsonObject,

    /// <summary>Force a response conforming to the specified JSON Schema.</summary>
    JsonSchema,
}
