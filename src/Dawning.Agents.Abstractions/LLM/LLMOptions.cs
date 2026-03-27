using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// LLM configuration options.
/// Supports configuration via appsettings.json, environment variables, user secrets, etc.
/// </summary>
/// <remarks>
/// appsettings.json example:
/// <code>
/// {
///   "LLM": {
///     "ProviderType": "Ollama",
///     "Model": "deepseek-coder:1.3b",
///     "Endpoint": "http://localhost:11434"
///   }
/// }
/// </code>
///
/// Environment variable examples:
/// - LLM__ProviderType=OpenAI
/// - LLM__Model=gpt-4o
/// - LLM__ApiKey=sk-xxx
///
/// Or use legacy environment variables (backward compatible):
/// - OPENAI_API_KEY, AZURE_OPENAI_ENDPOINT, etc.
/// </remarks>
public class LLMOptions : IValidatableOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "LLM";

    /// <summary>Provider type.</summary>
    public LLMProviderType ProviderType { get; set; } = LLMProviderType.Ollama;

    /// <summary>Model name or deployment name.</summary>
    public string Model { get; set; } = "deepseek-coder:1.3b";

    /// <summary>API key (OpenAI / Azure OpenAI).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Endpoint URL (Ollama / Azure OpenAI).</summary>
    public string? Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Validates that the configuration is valid.
    /// </summary>
    public void Validate()
    {
        switch (ProviderType)
        {
            case LLMProviderType.OpenAI:
                if (string.IsNullOrWhiteSpace(ApiKey))
                {
                    throw new InvalidOperationException("OpenAI requires ApiKey to be configured.");
                }
                break;

            case LLMProviderType.AzureOpenAI:
                if (string.IsNullOrWhiteSpace(Endpoint))
                {
                    throw new InvalidOperationException(
                        "Azure OpenAI requires Endpoint to be configured."
                    );
                }
                if (string.IsNullOrWhiteSpace(ApiKey))
                {
                    throw new InvalidOperationException(
                        "Azure OpenAI requires ApiKey to be configured."
                    );
                }
                break;

            case LLMProviderType.Ollama:
                if (string.IsNullOrWhiteSpace(Endpoint))
                {
                    throw new InvalidOperationException(
                        "Ollama requires Endpoint to be configured."
                    );
                }
                break;
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("Model must be configured.");
        }
    }
}
