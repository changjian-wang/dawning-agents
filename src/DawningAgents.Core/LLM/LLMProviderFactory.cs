using DawningAgents.Abstractions.LLM;
using DawningAgents.Azure;
using DawningAgents.OpenAI;

namespace DawningAgents.Core.LLM;

/// <summary>
/// LLM 提供者工厂
/// </summary>
public static class LLMProviderFactory
{
    /// <summary>
    /// 根据配置创建 LLM 提供者
    /// </summary>
    public static ILLMProvider Create(LLMConfiguration config)
    {
        return config.ProviderType switch
        {
            LLMProviderType.Ollama => new OllamaProvider(
                config.Model,
                config.Endpoint ?? "http://localhost:11434"),

            LLMProviderType.OpenAI => new OpenAIProvider(
                config.ApiKey ?? throw new InvalidOperationException("OpenAI API Key is required"),
                config.Model),

            LLMProviderType.AzureOpenAI => new AzureOpenAIProvider(
                config.Endpoint ?? throw new InvalidOperationException("Azure OpenAI endpoint is required"),
                config.ApiKey ?? throw new InvalidOperationException("Azure OpenAI API Key is required"),
                config.Model),

            _ => throw new ArgumentException($"Unknown provider type: {config.ProviderType}")
        };
    }

    /// <summary>
    /// 从环境变量自动创建 LLM 提供者
    /// </summary>
    public static ILLMProvider CreateFromEnvironment()
    {
        var config = LLMConfiguration.FromEnvironment();
        return Create(config);
    }
}
