namespace Dawning.Agents.Abstractions.Logging;

/// <summary>
/// 日志级别控制器接口
/// </summary>
/// <remarks>
/// 允许在运行时动态调整日志级别，无需重启应用。
/// </remarks>
public interface ILogLevelController
{
    /// <summary>
    /// 当前日志级别
    /// </summary>
    string CurrentLevel { get; }

    /// <summary>
    /// 设置日志级别
    /// </summary>
    /// <param name="level">日志级别（Verbose, Debug, Information, Warning, Error, Fatal）</param>
    void SetLevel(string level);

    /// <summary>
    /// 临时提升日志级别（用于调试）
    /// </summary>
    /// <param name="level">目标级别</param>
    /// <param name="duration">持续时间</param>
    /// <returns>恢复句柄，Dispose 后恢复原级别</returns>
    IDisposable TemporaryLevel(string level, TimeSpan duration);
}
