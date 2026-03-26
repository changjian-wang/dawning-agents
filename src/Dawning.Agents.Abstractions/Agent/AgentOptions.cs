using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent configuration options.
/// </summary>
/// <remarks>
/// Example appsettings.json configuration:
/// <code>
/// {
///   "Agent": {
///     "Name": "MyAgent",
///     "Instructions": "You are a helpful assistant.",
///     "MaxSteps": 10
///   }
/// }
/// </code>
/// </remarks>
public class AgentOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Agent";

    /// <summary>
    /// Agent name.
    /// </summary>
    public string Name { get; set; } = "Agent";

    /// <summary>
    /// Agent system instructions.
    /// </summary>
    public string Instructions { get; set; } = "You are a helpful AI assistant.";

    /// <summary>
    /// Maximum number of execution steps.
    /// </summary>
    public int MaxSteps { get; set; } = 10;

    /// <summary>
    /// Maximum tokens per LLM call.
    /// </summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// Maximum cost per run (USD). <c>null</c> means no limit.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="BudgetExceededException"/> when accumulated cost exceeds this value.
    /// </remarks>
    public decimal? MaxCostPerRun { get; set; }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Agent Name is required");
        }

        if (string.IsNullOrWhiteSpace(Instructions))
        {
            throw new InvalidOperationException("Agent Instructions is required");
        }

        if (MaxSteps <= 0)
        {
            throw new InvalidOperationException("MaxSteps must be greater than 0");
        }

        if (MaxTokens <= 0)
        {
            throw new InvalidOperationException("MaxTokens must be greater than 0");
        }

        if (MaxCostPerRun is <= 0)
        {
            throw new InvalidOperationException("MaxCostPerRun must be greater than 0");
        }
    }
}
