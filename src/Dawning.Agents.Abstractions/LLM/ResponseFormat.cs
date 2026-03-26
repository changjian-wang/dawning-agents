namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// LLM response format configuration.
/// </summary>
public record ResponseFormat
{
    /// <summary>Response format type.</summary>
    public ResponseFormatType Type { get; init; } = ResponseFormatType.Text;

    /// <summary>JSON Schema name (used only for the JsonSchema type).</summary>
    public string? SchemaName { get; init; }

    /// <summary>JSON Schema definition (used only for the JsonSchema type).</summary>
    public string? Schema { get; init; }

    /// <summary>Whether to enable strict mode (used only for the JsonSchema type).</summary>
    public bool Strict { get; init; }

    /// <summary>Plain text format.</summary>
    public static readonly ResponseFormat Text = new() { Type = ResponseFormatType.Text };

    /// <summary>JSON object format.</summary>
    public static readonly ResponseFormat JsonObject = new()
    {
        Type = ResponseFormatType.JsonObject,
    };

    /// <summary>
    /// Creates a JSON Schema format.
    /// </summary>
    public static ResponseFormat JsonSchema(string schemaName, string schema, bool strict = true) =>
        new()
        {
            Type = ResponseFormatType.JsonSchema,
            SchemaName = schemaName,
            Schema = schema,
            Strict = strict,
        };
}
