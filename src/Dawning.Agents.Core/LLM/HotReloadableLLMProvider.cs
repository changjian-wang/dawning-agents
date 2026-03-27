using Dawning.Agents.Abstractions.Configuration;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.LLM;

/// <summary>
/// A decorator for <see cref="ILLMProvider"/> that supports configuration hot-reload.
/// </summary>
/// <remarks>
/// <para>
/// This decorator wraps an <see cref="ILLMProvider"/>, monitors configuration changes, and automatically rebuilds the provider instance.
/// </para>
/// <para>
/// Suitable for scenarios that require modifying LLM configuration at runtime:
/// - Switching models (e.g., from qwen2.5:0.5b to qwen2.5:7b)
/// - Adjusting default parameters (Temperature, MaxTokens, etc.)
/// - Changing the endpoint URL
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddHotReloadableLLMProvider(configuration);
///
/// // After modifying appsettings.json, the provider is automatically updated
/// </code>
/// </example>
public sealed class HotReloadableLLMProvider : ILLMProvider, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HotReloadableLLMProvider> _logger;
    private readonly IDisposable? _changeTokenRegistration;
    private readonly Lock _lock = new();
    private readonly List<IDisposable> _retiredProviders = [];
    private readonly CancellationTokenSource _gracePeriodCts = new();
    private volatile ILLMProvider _innerProvider;
    private volatile bool _disposed;

    /// <summary>
    /// Occurs when the LLM configuration changes.
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

        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<HotReloadableLLMProvider>();

        // Create the initial provider
        _innerProvider = CreateProvider(optionsMonitor.CurrentValue);

        // Listen for configuration changes
        _changeTokenRegistration = optionsMonitor.OnChange(OnOptionsChanged);

        _logger.LogInformation("HotReloadableLLMProvider initialized, Provider: {Provider}", Name);
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

    public IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        return GetProvider().ChatStreamEventsAsync(messages, options, cancellationToken);
    }

    private ILLMProvider GetProvider()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _innerProvider;
    }

    private void OnOptionsChanged(LLMOptions newOptions, string? name)
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation(
            "LLM configuration changed, rebuilding provider. New configuration: Model={Model}, ProviderType={ProviderType}",
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

                // Defer disposal of the old provider to avoid ObjectDisposedException for in-flight requests
                if (oldProvider is IDisposable disposable)
                {
                    _retiredProviders.Add(disposable);
                    _ = DisposeAfterGracePeriodAsync(disposable);
                }
            }

            _logger.LogInformation(
                "LLM provider rebuilt successfully, new Provider: {Provider}",
                _innerProvider.Name
            );

            try
            {
                ConfigurationChanged?.Invoke(this, newOptions);
            }
            catch (Exception eventEx)
            {
                _logger.LogWarning(eventEx, "ConfigurationChanged event handler threw an exception");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild LLM provider; continuing with the existing provider");
        }
    }

    private ILLMProvider CreateProvider(LLMOptions options)
    {
        if (options.ProviderType != LLMProviderType.Ollama)
        {
            throw new InvalidOperationException(
                $"HotReloadableLLMProvider only supports the Ollama provider. "
                    + $"For {options.ProviderType}, use the corresponding extension package."
            );
        }

        var httpClient = _httpClientFactory.CreateClient("Ollama");
        var endpoint = options.Endpoint ?? "http://localhost:11434";
        httpClient.BaseAddress = new Uri(endpoint.TrimEnd('/'));
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        var logger = _loggerFactory.CreateLogger<OllamaProvider>();

        return new OllamaProvider(httpClient, options.Model, logger);
    }

    private async Task DisposeAfterGracePeriodAsync(IDisposable disposable)
    {
        try
        {
            // Wait long enough for in-flight requests to complete
            await Task.Delay(TimeSpan.FromSeconds(30), _gracePeriodCts.Token).ConfigureAwait(false);

            lock (_lock)
            {
                if (_disposed)
                {
                    // Already cleaned up by Dispose(); no need to dispose again
                    return;
                }

                _retiredProviders.Remove(disposable);
            }

            disposable.Dispose();
            _logger.LogDebug("Disposed old LLM provider instance");
        }
        catch (OperationCanceledException)
        {
            // Grace period cancelled during Dispose — expected
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing old LLM provider instance");
        }
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
            _gracePeriodCts.Cancel();
            _changeTokenRegistration?.Dispose();

            if (_innerProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            foreach (var retired in _retiredProviders)
            {
                retired.Dispose();
            }
            _retiredProviders.Clear();
            _gracePeriodCts.Dispose();

            _logger.LogDebug("HotReloadableLLMProvider disposed");
        }
    }
}
