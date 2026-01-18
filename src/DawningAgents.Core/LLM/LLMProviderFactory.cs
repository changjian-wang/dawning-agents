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
    /// 根据配置选项创建 LLM 提供者
    /// </summary>
    public static ILLMProvider Create(LLMOptions options)
    {
        return options.ProviderType switch
        {
            LLMProviderType.Ollama => new OllamaProvider(
                options.Model,
                options.Endpoint ?? "http://localhost:11434"),

            LLMProviderType.OpenAI => new OpenAIProvider(
                options.ApiKey ?? throw new InvalidOperationException("OpenAI API Key is required"),
                options.Model),

            LLMProviderType.AzureOpenAI => new AzureOpenAIProvider(
                options.Endpoint ?? throw new InvalidOperationException("Azure OpenAI endpoint is required"),
                options.ApiKey ?? throw new InvalidOperationException("Azure OpenAI API Key is required"),
                options.Model),

            _ => throw new ArgumentException($"Unknown provider type: {options.ProviderType}")
        };
    }

    /// <summary>
    /// 根据配置创建 LLM 提供者（向后兼容）
    /// </summary>
    [Obsolete("建议使用 Create(LLMOptions) 重载")]
    public static ILLMProvider Create(LLMConfiguration config)
    {
        return Create(config.ToOptions());
    }

    /// <summary>
    /// 从环境变量自动创建 LLM 提供者
    /// </summary>
    [Obsolete("建议使用依赖注入配合 IConfiguration")]
    public static ILLMProvider CreateFromEnvironment()
    {
#pragma warning disable CS0618
        var config = LLMConfiguration.FromEnvironment();
        return Create(config);
#pragma warning restore CS0618
    }
}
