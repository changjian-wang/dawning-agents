using Dawning.Agents.Abstractions.Configuration;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.LLM;

/// <summary>
/// 支持配置热重载的 LLM Provider 装饰器
/// </summary>
/// <remarks>
/// <para>
/// 此装饰器包装 <see cref="ILLMProvider"/>，监听配置变化并自动重建 Provider 实例。
/// </para>
/// <para>
/// 适用于需要运行时修改 LLM 配置的场景：
/// - 切换模型（如从 qwen2.5:0.5b 切换到 qwen2.5:7b）
/// - 调整默认参数（Temperature、MaxTokens 等）
/// - 更换 Endpoint 地址
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddHotReloadableLLMProvider(configuration);
///
/// // 修改 appsettings.json 后，Provider 会自动更新
/// </code>
/// </example>
public sealed class HotReloadableLLMProvider : ILLMProvider, IDisposable
{
    private readonly IOptionsMonitor<LLMOptions> _optionsMonitor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HotReloadableLLMProvider> _logger;
    private readonly IDisposable? _changeTokenRegistration;
    private readonly object _lock = new();
    private volatile ILLMProvider _innerProvider;
    private bool _disposed;

    /// <summary>
    /// 配置变化时触发的事件
    /// </summary>
    public event EventHandler<LLMOptions>? ConfigurationChanged;

    public string Name => _innerProvider.Name;

    public HotReloadableLLMProvider(
        IOptionsMonitor<LLMOptions> optionsMonitor,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory? loggerFactory = null
    )
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _optionsMonitor = optionsMonitor;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<HotReloadableLLMProvider>();

        // 创建初始 Provider
        _innerProvider = CreateProvider(optionsMonitor.CurrentValue);

        // 监听配置变化
        _changeTokenRegistration = optionsMonitor.OnChange(OnOptionsChanged);

        _logger.LogInformation("HotReloadableLLMProvider 已初始化，Provider: {Provider}", Name);
    }

    public Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        return GetProvider().ChatAsync(messages, options, cancellationToken);
    }

    public IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        return GetProvider().ChatStreamAsync(messages, options, cancellationToken);
    }

    private ILLMProvider GetProvider()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HotReloadableLLMProvider));
        }

        return _innerProvider;
    }

    private void OnOptionsChanged(LLMOptions newOptions, string? name)
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation(
            "LLM 配置已变更，正在重建 Provider，新配置: Model={Model}, ProviderType={ProviderType}",
            newOptions.Model,
            newOptions.ProviderType
        );

        try
        {
            newOptions.Validate();

            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                var oldProvider = _innerProvider;
                _innerProvider = CreateProvider(newOptions);

                // 如果旧 Provider 是 IDisposable，则释放它
                if (oldProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _logger.LogInformation(
                "LLM Provider 已重建成功，新 Provider: {Provider}",
                _innerProvider.Name
            );

            ConfigurationChanged?.Invoke(this, newOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重建 LLM Provider 失败，将继续使用旧 Provider");
        }
    }

    private ILLMProvider CreateProvider(LLMOptions options)
    {
        if (options.ProviderType != LLMProviderType.Ollama)
        {
            throw new InvalidOperationException(
                $"HotReloadableLLMProvider 仅支持 Ollama Provider。"
                    + $"对于 {options.ProviderType}，请使用对应的扩展包。"
            );
        }

        var httpClient = _httpClientFactory.CreateClient("Ollama");
        var endpoint = options.Endpoint ?? "http://localhost:11434";
        httpClient.BaseAddress = new Uri(endpoint.TrimEnd('/'));
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        var logger = _loggerFactory.CreateLogger<OllamaProvider>();

        return new OllamaProvider(httpClient, options.Model, logger);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _changeTokenRegistration?.Dispose();

            if (_innerProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _logger.LogDebug("HotReloadableLLMProvider 已释放");
        }
    }
}
