using DawningAgents.Abstractions.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DawningAgents.Core.LLM;

/// <summary>
/// ILLMProvider 的依赖注入扩展
/// </summary>
public static class LLMServiceCollectionExtensions
{
    /// <summary>
    /// 从 IConfiguration 添加 LLM Provider 服务
    /// 支持 appsettings.json、环境变量、用户机密等配置源
    /// </summary>
    /// <remarks>
    /// appsettings.json 示例:
    /// <code>
    /// {
    ///   "LLM": {
    ///     "ProviderType": "Ollama",
    ///     "Model": "deepseek-coder:1.3B",
    ///     "Endpoint": "http://localhost:11434"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddLLMProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        services.Configure<LLMOptions>(configuration.GetSection(LLMOptions.SectionName));

        // 检查是否有配置，如果没有则回退到环境变量
        var section = configuration.GetSection(LLMOptions.SectionName);
        if (!section.Exists())
        {
            // 回退到传统环境变量检测
            services.PostConfigure<LLMOptions>(options =>
            {
                ApplyEnvironmentVariables(options);
            });
        }

        // 注册 Provider
        services.TryAddSingleton<ILLMProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
            options.Validate();
            return LLMProviderFactory.Create(options);
        });

        return services;
    }

    /// <summary>
    /// 添加 LLM Provider 服务（从环境变量自动配置）
    /// </summary>
    [Obsolete("建议使用 AddLLMProvider(IConfiguration) 重载")]
    public static IServiceCollection AddLLMProvider(this IServiceCollection services)
    {
#pragma warning disable CS0618
        services.TryAddSingleton(_ => LLMConfiguration.FromEnvironment());
        services.TryAddSingleton<ILLMProvider>(sp =>
        {
            var config = sp.GetRequiredService<LLMConfiguration>();
            return LLMProviderFactory.Create(config);
        });
#pragma warning restore CS0618
        return services;
    }

    /// <summary>
    /// 添加 LLM Provider 服务（使用指定配置）
    /// </summary>
    [Obsolete("建议使用 AddLLMProvider(IConfiguration) 重载")]
    public static IServiceCollection AddLLMProvider(
        this IServiceCollection services,
        LLMConfiguration configuration)
    {
        services.TryAddSingleton(configuration);
        services.TryAddSingleton<ILLMProvider>(sp =>
        {
#pragma warning disable CS0618
            var config = sp.GetRequiredService<LLMConfiguration>();
            return LLMProviderFactory.Create(config);
#pragma warning restore CS0618
        });
        return services;
    }

    /// <summary>
    /// 添加 LLM Provider 服务（使用配置委托）
    /// </summary>
    public static IServiceCollection AddLLMProvider(
        this IServiceCollection services,
        Action<LLMOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<ILLMProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
            options.Validate();
            return LLMProviderFactory.Create(options);
        });
        return services;
    }

    /// <summary>
    /// 添加 Ollama Provider
    /// </summary>
    public static IServiceCollection AddOllamaProvider(
        this IServiceCollection services,
        string model = "deepseek-coder:1.3B",
        string endpoint = "http://localhost:11434")
    {
        return services.AddLLMProvider(options =>
        {
            options.ProviderType = LLMProviderType.Ollama;
            options.Model = model;
            options.Endpoint = endpoint;
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
        return services.AddLLMProvider(options =>
        {
            options.ProviderType = LLMProviderType.OpenAI;
            options.ApiKey = apiKey;
            options.Model = model;
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
        return services.AddLLMProvider(options =>
        {
            options.ProviderType = LLMProviderType.AzureOpenAI;
            options.Endpoint = endpoint;
            options.ApiKey = apiKey;
            options.Model = deploymentName;
        });
    }

    /// <summary>
    /// 应用传统环境变量到配置（向后兼容）
    /// </summary>
    private static void ApplyEnvironmentVariables(LLMOptions options)
    {
        // 如果已经有配置，不覆盖
        if (!string.IsNullOrEmpty(options.ApiKey) || options.ProviderType != LLMProviderType.Ollama)
        {
            return;
        }

        // 优先检查 Azure OpenAI
        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

        if (!string.IsNullOrEmpty(azureEndpoint) && !string.IsNullOrEmpty(azureApiKey))
        {
            options.ProviderType = LLMProviderType.AzureOpenAI;
            options.Endpoint = azureEndpoint;
            options.ApiKey = azureApiKey;
            options.Model = azureDeployment ?? "gpt-4o";
            return;
        }

        // 检查 OpenAI
        var openaiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var openaiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");

        if (!string.IsNullOrEmpty(openaiApiKey))
        {
            options.ProviderType = LLMProviderType.OpenAI;
            options.ApiKey = openaiApiKey;
            options.Model = openaiModel ?? "gpt-4o";
            return;
        }

        // 默认 Ollama
        var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
        var ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL");

        options.Endpoint = ollamaEndpoint ?? options.Endpoint ?? "http://localhost:11434";
        options.Model = ollamaModel ?? options.Model ?? "deepseek-coder:1.3B";
    }
}
