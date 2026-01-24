using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.OpenAI;

/// <summary>
/// OpenAI Provider 的依赖注入扩展
/// </summary>
public static class OpenAIServiceCollectionExtensions
{
    /// <summary>
    /// 添加 OpenAI Provider
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="model">模型名称，默认 gpt-4o</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddOpenAIProvider("sk-xxx", "gpt-4o");
    /// </code>
    /// </example>
    public static IServiceCollection AddOpenAIProvider(
        this IServiceCollection services,
        string apiKey,
        string model = "gpt-4o"
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        services.TryAddSingleton<ILLMProvider>(_ => new OpenAIProvider(apiKey, model));

        return services;
    }

    /// <summary>
    /// 添加 OpenAI Provider（使用配置委托）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddOpenAIProvider(options =>
    /// {
    ///     options.ApiKey = "sk-xxx";
    ///     options.Model = "gpt-4o";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOpenAIProvider(
        this IServiceCollection services,
        Action<OpenAIProviderOptions> configure
    )
    {
        var options = new OpenAIProviderOptions();
        configure(options);
        options.Validate();

        return services.AddOpenAIProvider(options.ApiKey!, options.Model);
    }
}

/// <summary>
/// OpenAI Provider 配置选项
/// </summary>
public class OpenAIProviderOptions
{
    /// <summary>
    /// OpenAI API Key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = "gpt-4o";

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("OpenAI ApiKey is required");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("OpenAI Model is required");
        }
    }
}
