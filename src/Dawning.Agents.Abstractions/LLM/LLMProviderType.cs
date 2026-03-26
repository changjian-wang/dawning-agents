namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// LLM provider type.
/// </summary>
public enum LLMProviderType
{
    /// <summary>Local Ollama model.</summary>
    Ollama,

    /// <summary>OpenAI API.</summary>
    OpenAI,

    /// <summary>Azure OpenAI / Azure AI Foundry.</summary>
    AzureOpenAI,
}
