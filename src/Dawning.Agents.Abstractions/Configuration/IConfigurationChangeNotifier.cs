namespace Dawning.Agents.Abstractions.Configuration;

/// <summary>
/// Configuration change event arguments.
/// </summary>
/// <typeparam name="TOptions">The configuration type.</typeparam>
public class ConfigurationChangedEventArgs<TOptions> : EventArgs
    where TOptions : class
{
    /// <summary>
    /// Previous configuration value.
    /// </summary>
    public TOptions? OldValue { get; }

    /// <summary>
    /// New configuration value.
    /// </summary>
    public TOptions NewValue { get; }

    /// <summary>
    /// Configuration name (for named configurations).
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Change timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ConfigurationChangedEventArgs{TOptions}"/>.
    /// </summary>
    public ConfigurationChangedEventArgs(TOptions? oldValue, TOptions newValue, string? name = null)
    {
        OldValue = oldValue;
        NewValue = newValue;
        Name = name;
        Timestamp = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Configuration change notification interface.
/// </summary>
/// <typeparam name="TOptions">The configuration type.</typeparam>
public interface IConfigurationChangeNotifier<TOptions>
    where TOptions : class
{
    /// <summary>
    /// Configuration change event.
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs<TOptions>>? ConfigurationChanged;

    /// <summary>
    /// Current configuration value.
    /// </summary>
    TOptions CurrentValue { get; }
}
