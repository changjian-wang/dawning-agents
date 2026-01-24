namespace Dawning.Agents.Abstractions.Communication;

/// <summary>
/// 多 Agent 协作的共享状态存储接口
/// </summary>
/// <remarks>
/// 共享状态用于 Agent 之间交换数据，支持：
/// <list type="bullet">
/// <item>键值存储</item>
/// <item>模式匹配查询</item>
/// <item>变更通知</item>
/// </list>
/// </remarks>
public interface ISharedState
{
    /// <summary>
    /// 从共享状态获取值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">键名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>值，不存在时返回 null</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 在共享状态中设置值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">键名</param>
    /// <param name="value">值</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从共享状态删除值
    /// </summary>
    /// <param name="key">键名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功删除</returns>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查键是否存在
    /// </summary>
    /// <param name="key">键名</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取匹配模式的所有键
    /// </summary>
    /// <param name="pattern">匹配模式（支持 * 通配符）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匹配的键列表</returns>
    Task<IReadOnlyList<string>> GetKeysAsync(
        string pattern = "*",
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 订阅键的变更通知
    /// </summary>
    /// <param name="key">要监听的键</param>
    /// <param name="handler">变更处理器</param>
    /// <returns>取消订阅的 Disposable</returns>
    IDisposable OnChange(string key, Action<string, object?> handler);

    /// <summary>
    /// 清除所有共享状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前存储的键值对数量
    /// </summary>
    int Count { get; }
}
