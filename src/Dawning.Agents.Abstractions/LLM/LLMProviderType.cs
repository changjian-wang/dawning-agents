namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// LLM 提供者类型
/// </summary>
public enum LLMProviderType
{
    /// <summary>本地 Ollama 模型</summary>
    Ollama,

    /// <summary>OpenAI API</summary>
    OpenAI,

    /// <summary>Azure OpenAI / Azure AI Foundry</summary>
    AzureOpenAI,
}
