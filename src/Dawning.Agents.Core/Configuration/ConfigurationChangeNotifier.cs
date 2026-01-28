using Dawning.Agents.Abstractions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Configuration;

/// <summary>
/// 配置变更通知器 - 监听 IOptionsMonitor 变更并发出事件
/// </summary>
/// <typeparam name="TOptions">配置类型</typeparam>
public sealed class ConfigurationChangeNotifier<TOptions>
    : IConfigurationChangeNotifier<TOptions>,
        IDisposable
    where TOptions : class
{
    private readonly IOptionsMonitor<TOptions> _optionsMonitor;
    private readonly ILogger<ConfigurationChangeNotifier<TOptions>> _logger;
    private readonly IDisposable? _changeListener;
    private TOptions _currentValue;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<ConfigurationChangedEventArgs<TOptions>>? ConfigurationChanged;

    /// <inheritdoc />
    public TOptions CurrentValue => _optionsMonitor.CurrentValue;

    /// <summary>
    /// 创建配置变更通知器
    /// </summary>
    public ConfigurationChangeNotifier(
        IOptionsMonitor<TOptions> optionsMonitor,
        ILogger<ConfigurationChangeNotifier<TOptions>>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);

        _optionsMonitor = optionsMonitor;
        _logger = logger ?? NullLogger<ConfigurationChangeNotifier<TOptions>>.Instance;
        _currentValue = optionsMonitor.CurrentValue;

        // 监听配置变更
        _changeListener = optionsMonitor.OnChange(OnConfigurationChanged);

        _logger.LogDebug(
            "ConfigurationChangeNotifier created for {OptionsType}",
            typeof(TOptions).Name
        );
    }

    private void OnConfigurationChanged(TOptions newValue, string? name)
    {
        if (_disposed)
        {
            return;
        }

        var oldValue = _currentValue;
        _currentValue = newValue;

        _logger.LogInformation(
            "Configuration changed for {OptionsType}, Name={Name}",
            typeof(TOptions).Name,
            name ?? "(default)"
        );

        try
        {
            ConfigurationChanged?.Invoke(
                this,
                new ConfigurationChangedEventArgs<TOptions>(oldValue, newValue, name)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error handling configuration change for {OptionsType}",
                typeof(TOptions).Name
            );
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _changeListener?.Dispose();
    }
}
