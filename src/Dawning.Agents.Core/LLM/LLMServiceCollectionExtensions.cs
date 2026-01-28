using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Azure;
using Dawning.Agents.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.LLM;

/// <summary>
/// ILLMProvider 的依赖注入扩展
/// </summary>
/// <remarks>
/// 此扩展支持 Ollama（本地 LLM）、OpenAI 和 Azure OpenAI。
/// 根据配置中的 ProviderType 自动选择对应的 Provider。
/// </remarks>
public static class LLMServiceCollectionExtensions
{
    /// <summary>
    /// HttpClient 名称常量
    /// </summary>
    public const string OllamaHttpClientName = "Ollama";

    /// <summary>
    /// 从 IConfiguration 添加 LLM Provider 服务
    /// </summary>
    /// <remarks>
    /// <para>根据配置中的 ProviderType 自动选择 Provider:</para>
    /// <list type="bullet">
    ///   <item>Ollama - 本地 LLM</item>
    ///   <item>OpenAI - OpenAI API</item>
    ///   <item>AzureOpenAI - Azure OpenAI Service</item>
    /// </list>
    /// <para>
    /// appsettings.json 示例 (Ollama):
    /// <code>
    /// {
    ///   "LLM": {
    ///     "ProviderType": "Ollama",
    ///     "Model": "qwen2.5:0.5b",
    ///     "Endpoint": "http://localhost:11434"
    ///   }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// appsettings.json 示例 (OpenAI):
    /// <code>
    /// {
    ///   "LLM": {
    ///     "ProviderType": "OpenAI",
    ///     "Model": "gpt-4o-mini",
    ///     "ApiKey": "sk-xxx"
    ///   }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// appsettings.json 示例 (Azure OpenAI):
    /// <code>
    /// {
    ///   "LLM": {
    ///     "ProviderType": "AzureOpenAI",
    ///     "Endpoint": "https://your-resource.openai.azure.com",
    ///     "Model": "gpt-4o-deployment",
    ///     "ApiKey": "your-api-key"
    ///   }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddLLMProvider(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // 绑定配置
        services.Configure<LLMOptions>(configuration.GetSection(LLMOptions.SectionName));

        // 检查是否有配置，如果没有则回退到环境变量
        var section = configuration.GetSection(LLMOptions.SectionName);
        if (!section.Exists())
        {
            services.PostConfigure<LLMOptions>(ApplyEnvironmentVariables);
        }

        // 注册 HttpClient（用于 Ollama）
        RegisterOllamaHttpClient(services);

        // 注册 Provider（根据配置类型自动选择）
        services.TryAddSingleton<ILLMProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
            options.Validate();
            return CreateProvider(sp, options);
        });

        return services;
    }

    /// <summary>
    /// 添加支持热重载的 LLM Provider
    /// </summary>
    /// <remarks>
    /// <para>此方法注册的 Provider 会自动响应配置变化。</para>
    /// <para>当 appsettings.json 中的 LLM 配置修改后，Provider 会自动重建。</para>
    /// <para>
    /// 适用场景：
    /// - 需要运行时切换模型
    /// - 需要动态调整参数（如 Temperature）
    /// - 开发/测试环境频繁修改配置
    /// </para>
    /// <para>
    /// appsettings.json 示例:
    /// <code>
    /// {
    ///   "LLM": {
    ///     "ProviderType": "Ollama",
    ///     "Model": "qwen2.5:0.5b",
    ///     "Endpoint": "http://localhost:11434"
    ///   }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddHotReloadableLLMProvider(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // 绑定配置
        services.Configure<LLMOptions>(configuration.GetSection(LLMOptions.SectionName));

        // 检查是否有配置，如果没有则回退到环境变量
        var section = configuration.GetSection(LLMOptions.SectionName);
        if (!section.Exists())
        {
            services.PostConfigure<LLMOptions>(ApplyEnvironmentVariables);
        }

        // 注册 HttpClient（用于 Ollama）
        RegisterOllamaHttpClient(services);

        // 注册支持热重载的 Provider
        services.TryAddSingleton<ILLMProvider, HotReloadableLLMProvider>();

        return services;
    }

    /// <summary>
    /// 添加 LLM Provider 服务（使用配置委托）
    /// </summary>
    public static IServiceCollection AddLLMProvider(
        this IServiceCollection services,
        Action<LLMOptions> configure
    )
    {
        services.Configure(configure);

        // 注册 HttpClient
        RegisterOllamaHttpClient(services);

        services.TryAddSingleton<ILLMProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
            options.Validate();
            return CreateProvider(sp, options);
        });

        return services;
    }

    /// <summary>
    /// 添加 Ollama Provider（本地 LLM）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="model">模型名称</param>
    /// <param name="endpoint">Ollama 端点地址</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddOllamaProvider("qwen2.5:0.5b");
    /// </code>
    /// </example>
    public static IServiceCollection AddOllamaProvider(
        this IServiceCollection services,
        string model = "qwen2.5:0.5b",
        string endpoint = "http://localhost:11434"
    )
    {
        return services.AddLLMProvider(options =>
        {
            options.ProviderType = LLMProviderType.Ollama;
            options.Model = model;
            options.Endpoint = endpoint;
        });
    }

    private static void RegisterOllamaHttpClient(IServiceCollection services)
    {
        services.AddHttpClient(
            OllamaHttpClientName,
            (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
                var endpoint = options.Endpoint ?? "http://localhost:11434";
                client.BaseAddress = new Uri(endpoint.TrimEnd('/'));
                client.Timeout = TimeSpan.FromMinutes(5);
            }
        );
    }

    /// <summary>
    /// 根据配置创建对应的 Provider
    /// </summary>
    private static ILLMProvider CreateProvider(IServiceProvider sp, LLMOptions options)
    {
        var loggerFactory = sp.GetService<ILoggerFactory>();

        return options.ProviderType switch
        {
            LLMProviderType.Ollama => CreateOllamaProvider(sp, options),
            LLMProviderType.OpenAI => CreateOpenAIProvider(options, loggerFactory),
            LLMProviderType.AzureOpenAI => CreateAzureOpenAIProvider(options, loggerFactory),
            _ => throw new NotSupportedException($"不支持的 Provider 类型: {options.ProviderType}"),
        };
    }

    private static OllamaProvider CreateOllamaProvider(IServiceProvider sp, LLMOptions options)
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(OllamaHttpClientName);
        var loggerFactory = sp.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger<OllamaProvider>();

        return new OllamaProvider(httpClient, options.Model, logger);
    }

    private static Dawning.Agents.OpenAI.OpenAIProvider CreateOpenAIProvider(
        LLMOptions options,
        ILoggerFactory? loggerFactory
    )
    {
        return new Dawning.Agents.OpenAI.OpenAIProvider(options.ApiKey!, options.Model);
    }

    private static Dawning.Agents.Azure.AzureOpenAIProvider CreateAzureOpenAIProvider(
        LLMOptions options,
        ILoggerFactory? loggerFactory
    )
    {
        return new Dawning.Agents.Azure.AzureOpenAIProvider(
            options.Endpoint!,
            options.ApiKey!,
            options.Model
        );
    }

    /// <summary>
    /// 应用环境变量到配置
    /// </summary>
    private static void ApplyEnvironmentVariables(LLMOptions options)
    {
        // OpenAI 环境变量
        var openaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openaiKey) && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            options.ProviderType = LLMProviderType.OpenAI;
            options.ApiKey = openaiKey;
            options.Model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
            return;
        }

        // Azure OpenAI 环境变量
        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(azureEndpoint) && !string.IsNullOrWhiteSpace(azureKey))
        {
            options.ProviderType = LLMProviderType.AzureOpenAI;
            options.Endpoint = azureEndpoint;
            options.ApiKey = azureKey;
            options.Model =
                Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? options.Model;
            return;
        }

        // Ollama 环境变量（默认）
        var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
        var ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL");

        options.Endpoint = ollamaEndpoint ?? options.Endpoint ?? "http://localhost:11434";
        options.Model = ollamaModel ?? options.Model ?? "qwen2.5:0.5b";
    }
}
