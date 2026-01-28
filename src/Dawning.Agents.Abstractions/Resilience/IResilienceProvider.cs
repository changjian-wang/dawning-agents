namespace Dawning.Agents.Abstractions.Resilience;

/// <summary>
/// 弹性策略提供者接口
/// </summary>
/// <remarks>
/// 提供重试、断路器、超时等弹性策略，保护 LLM 调用。
/// </remarks>
public interface IResilienceProvider
{
    /// <summary>
    /// 使用弹性策略执行操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="operation">要执行的操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 使用弹性策略执行操作（无返回值）
    /// </summary>
    /// <param name="operation">要执行的操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    );
}
