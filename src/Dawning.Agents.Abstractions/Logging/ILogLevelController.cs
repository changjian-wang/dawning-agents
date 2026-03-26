namespace Dawning.Agents.Abstractions.Logging;

/// <summary>
/// Defines the interface for runtime log level control.
/// </summary>
/// <remarks>
/// Allows dynamic adjustment of log levels at runtime without restarting the application.
/// </remarks>
public interface ILogLevelController
{
    /// <summary>
    /// The current log level.
    /// </summary>
    string CurrentLevel { get; }

    /// <summary>
    /// Sets the log level.
    /// </summary>
    /// <param name="level">The log level (Verbose, Debug, Information, Warning, Error, Fatal).</param>
    void SetLevel(string level);

    /// <summary>
    /// Temporarily elevates the log level for debugging purposes.
    /// </summary>
    /// <param name="level">The target log level.</param>
    /// <param name="duration">The duration of the temporary level.</param>
    /// <returns>A handle that restores the previous log level when disposed.</returns>
    IDisposable TemporaryLevel(string level, TimeSpan duration);
}
