namespace Dawning.Agents.Abstractions.Configuration;

/// <summary>
/// 配置变更事件参数
/// </summary>
/// <typeparam name="TOptions">配置类型</typeparam>
public class ConfigurationChangedEventArgs<TOptions> : EventArgs
    where TOptions : class
{
    /// <summary>
    /// 旧配置
    /// </summary>
    public TOptions? OldValue { get; }

    /// <summary>
    /// 新配置
    /// </summary>
    public TOptions NewValue { get; }

    /// <summary>
    /// 配置名称（用于命名配置）
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// 变更时间戳
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// 创建配置变更事件参数
    /// </summary>
    public ConfigurationChangedEventArgs(TOptions? oldValue, TOptions newValue, string? name = null)
    {
        OldValue = oldValue;
        NewValue = newValue;
        Name = name;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// 配置变更通知接口
/// </summary>
/// <typeparam name="TOptions">配置类型</typeparam>
public interface IConfigurationChangeNotifier<TOptions>
    where TOptions : class
{
    /// <summary>
    /// 配置变更事件
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs<TOptions>>? ConfigurationChanged;

    /// <summary>
    /// 当前配置值
    /// </summary>
    TOptions CurrentValue { get; }
}
