using Azure.Core;
using Dawning.Agents.Abstractions;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Dawning.Agents.Azure;

/// <summary>
/// Azure OpenAI Provider 的依赖注入扩展
/// </summary>
public static class AzureOpenAIServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Azure OpenAI Provider（API Key 认证）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="endpoint">Azure OpenAI 端点</param>
    /// <param name="apiKey">API Key</param>
    /// <param name="deploymentName">部署名称</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddAzureOpenAIProvider(
    ///     "https://your-resource.openai.azure.com/",
    ///     "your-api-key",
    ///     "gpt-4o"
    /// );
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureOpenAIProvider(
        this IServiceCollection services,
        string endpoint,
        string apiKey,
        string deploymentName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        services.TryAddSingleton<ILLMProvider>(sp => new AzureOpenAIProvider(
            endpoint,
            apiKey,
            deploymentName,
            sp.GetService<ILoggerFactory>()?.CreateLogger<AzureOpenAIProvider>()
        ));

        return services;
    }

    /// <summary>
    /// 添加 Azure OpenAI Provider（Azure AD 认证）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="endpoint">Azure OpenAI 端点</param>
    /// <param name="credential">Azure 凭据</param>
    /// <param name="deploymentName">部署名称</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddAzureOpenAIProvider(
    ///     "https://your-resource.openai.azure.com/",
    ///     new DefaultAzureCredential(),
    ///     "gpt-4o"
    /// );
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureOpenAIProvider(
        this IServiceCollection services,
        string endpoint,
        TokenCredential credential,
        string deploymentName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        services.TryAddSingleton<ILLMProvider>(sp => new AzureOpenAIProvider(
            endpoint,
            credential,
            deploymentName,
            sp.GetService<ILoggerFactory>()?.CreateLogger<AzureOpenAIProvider>()
        ));

        return services;
    }

    /// <summary>
    /// 添加 Azure OpenAI Provider（使用配置委托）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddAzureOpenAIProvider(options =>
    /// {
    ///     options.Endpoint = "https://your-resource.openai.azure.com/";
    ///     options.ApiKey = "your-api-key";
    ///     options.DeploymentName = "gpt-4o";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureOpenAIProvider(
        this IServiceCollection services,
        Action<AzureOpenAIProviderOptions> configure
    )
    {
        var options = new AzureOpenAIProviderOptions();
        configure(options);
        options.Validate();

        return services.AddAzureOpenAIProvider(
            options.Endpoint!,
            options.ApiKey!,
            options.DeploymentName!
        );
    }

    /// <summary>
    /// 添加 Azure OpenAI Embedding Provider
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="endpoint">Azure OpenAI 端点</param>
    /// <param name="apiKey">Azure OpenAI API Key</param>
    /// <param name="deploymentName">嵌入模型部署名称</param>
    /// <param name="dimensions">向量维度</param>
    public static IServiceCollection AddAzureOpenAIEmbedding(
        this IServiceCollection services,
        string endpoint,
        string apiKey,
        string deploymentName,
        int dimensions = 1536
    )
    {
        services.AddSingleton<IEmbeddingProvider>(sp => new AzureOpenAIEmbeddingProvider(
            endpoint,
            apiKey,
            deploymentName,
            dimensions,
            sp.GetService<ILoggerFactory>()?.CreateLogger<AzureOpenAIEmbeddingProvider>()
        ));
        return services;
    }
}

/// <summary>
/// Azure OpenAI Provider 配置选项
/// </summary>
public class AzureOpenAIProviderOptions : IValidatableOptions
{
    /// <summary>
    /// Azure OpenAI 端点
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// API Key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 部署名称
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException("Azure OpenAI Endpoint is required");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Azure OpenAI ApiKey is required");
        }

        if (string.IsNullOrWhiteSpace(DeploymentName))
        {
            throw new InvalidOperationException("Azure OpenAI DeploymentName is required");
        }
    }
}
