using Dawning.Agents.Abstractions.LLM;
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
/// 此扩展提供 Ollama（本地 LLM）的支持。
/// 对于 OpenAI，请使用 Dawning.Agents.OpenAI 包的 AddOpenAIProvider 方法。
/// 对于 Azure OpenAI，请使用 Dawning.Agents.Azure 包的 AddAzureOpenAIProvider 方法。
/// </remarks>
public static class LLMServiceCollectionExtensions
{
    private const string OllamaHttpClientName = "Ollama";

    /// <summary>
    /// 从 IConfiguration 添加 LLM Provider 服务
    /// </summary>
    /// <remarks>
    /// <para>此方法仅支持 Ollama Provider。</para>
    /// <para>对于 OpenAI/Azure OpenAI，请分别使用对应包的扩展方法。</para>
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

        // 注册 Provider
        services.TryAddSingleton<ILLMProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
            options.Validate();

            if (options.ProviderType != LLMProviderType.Ollama)
            {
                throw new InvalidOperationException(
                    $"Dawning.Agents.Core 仅支持 Ollama Provider。"
                        + $"对于 {options.ProviderType}，请使用对应的扩展包："
                        + $"OpenAI -> Dawning.Agents.OpenAI, "
                        + $"AzureOpenAI -> Dawning.Agents.Azure"
                );
            }

            return CreateOllamaProvider(sp, options);
        });

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

        services.TryAddSingleton<ILLMProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
            options.Validate();

            if (options.ProviderType != LLMProviderType.Ollama)
            {
                throw new InvalidOperationException(
                    $"Dawning.Agents.Core 仅支持 Ollama Provider。"
                        + $"对于 {options.ProviderType}，请使用对应的扩展包："
                        + $"OpenAI -> Dawning.Agents.OpenAI, "
                        + $"AzureOpenAI -> Dawning.Agents.Azure"
                );
            }

            return CreateOllamaProvider(sp, options);
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

    private static OllamaProvider CreateOllamaProvider(IServiceProvider sp, LLMOptions options)
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(OllamaHttpClientName);
        var loggerFactory = sp.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger<OllamaProvider>();

        return new OllamaProvider(httpClient, options.Model, logger);
    }

    /// <summary>
    /// 应用环境变量到配置（用于 Ollama）
    /// </summary>
    private static void ApplyEnvironmentVariables(LLMOptions options)
    {
        // 如果已经有配置，不覆盖
        if (options.ProviderType != LLMProviderType.Ollama)
        {
            return;
        }

        var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
        var ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL");

        options.Endpoint = ollamaEndpoint ?? options.Endpoint ?? "http://localhost:11434";
        options.Model = ollamaModel ?? options.Model ?? "qwen2.5:0.5b";
    }
}
