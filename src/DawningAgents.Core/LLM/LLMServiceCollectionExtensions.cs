using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DawningAgents.Core.LLM;

/// <summary>
/// ILLMProvider 的依赖注入扩展
/// </summary>
public static class LLMServiceCollectionExtensions
{
    /// <summary>
    /// 添加 LLM Provider 服务（从环境变量自动配置）
    /// </summary>
    public static IServiceCollection AddLLMProvider(this IServiceCollection services)
    {
        services.TryAddSingleton(_ => LLMConfiguration.FromEnvironment());
        services.TryAddSingleton<ILLMProvider>(sp =>
        {
            var config = sp.GetRequiredService<LLMConfiguration>();
            return LLMProviderFactory.Create(config);
        });
        return services;
    }

    /// <summary>
    /// 添加 LLM Provider 服务（使用指定配置）
    /// </summary>
    public static IServiceCollection AddLLMProvider(
        this IServiceCollection services,
        LLMConfiguration configuration)
    {
        services.TryAddSingleton(configuration);
        services.TryAddSingleton<ILLMProvider>(sp =>
        {
            var config = sp.GetRequiredService<LLMConfiguration>();
            return LLMProviderFactory.Create(config);
        });
        return services;
    }

    /// <summary>
    /// 添加 LLM Provider 服务（使用配置委托）
    /// </summary>
    public static IServiceCollection AddLLMProvider(
        this IServiceCollection services,
        Action<LLMConfiguration> configure)
    {
        var config = new LLMConfiguration();
        configure(config);
        return services.AddLLMProvider(config);
    }

    /// <summary>
    /// 添加 Ollama Provider
    /// </summary>
    public static IServiceCollection AddOllamaProvider(
        this IServiceCollection services,
        string model = "deepseek-coder:6.7b",
        string endpoint = "http://localhost:11434")
    {
        return services.AddLLMProvider(new LLMConfiguration
        {
            ProviderType = LLMProviderType.Ollama,
            Model = model,
            Endpoint = endpoint
        });
    }

    /// <summary>
    /// 添加 OpenAI Provider
    /// </summary>
    public static IServiceCollection AddOpenAIProvider(
        this IServiceCollection services,
        string apiKey,
        string model = "gpt-4o")
    {
        return services.AddLLMProvider(new LLMConfiguration
        {
            ProviderType = LLMProviderType.OpenAI,
            ApiKey = apiKey,
            Model = model
        });
    }

    /// <summary>
    /// 添加 Azure OpenAI Provider
    /// </summary>
    public static IServiceCollection AddAzureOpenAIProvider(
        this IServiceCollection services,
        string endpoint,
        string apiKey,
        string deploymentName)
    {
        return services.AddLLMProvider(new LLMConfiguration
        {
            ProviderType = LLMProviderType.AzureOpenAI,
            Endpoint = endpoint,
            ApiKey = apiKey,
            Model = deploymentName
        });
    }
}
