namespace Dawning.Agents.Core.Communication;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Communication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// 内存共享状态实现
/// </summary>
/// <remarks>
/// 适用于单进程内的状态共享，支持：
/// <list type="bullet">
/// <item>类型安全的键值存储</item>
/// <item>通配符模式匹配</item>
/// <item>变更通知</item>
/// </list>
/// </remarks>
public partial class InMemorySharedState : ISharedState
{
    private readonly ConcurrentDictionary<string, string> _store = new();
    private readonly ConcurrentDictionary<string, List<Action<string, object?>>> _watchers = new();
    private readonly ILogger<InMemorySharedState> _logger;
    private readonly object _watcherLock = new();

    /// <summary>
    /// 创建内存共享状态
    /// </summary>
    public InMemorySharedState(ILogger<InMemorySharedState>? logger = null)
    {
        _logger = logger ?? NullLogger<InMemorySharedState>.Instance;
    }

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var json))
        {
            try
            {
                var value = JsonSerializer.Deserialize<T>(json);
                _logger.LogDebug("获取共享状态 {Key}", key);
                return Task.FromResult(value);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "反序列化共享状态 {Key} 失败", key);
            }
        }

        return Task.FromResult<T?>(default);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);
        _store[key] = json;

        _logger.LogDebug("设置共享状态 {Key}", key);

        // 通知变更
        NotifyChange(key, value);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var removed = _store.TryRemove(key, out _);

        if (removed)
        {
            _logger.LogDebug("删除共享状态 {Key}", key);
            NotifyChange(key, null);
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.ContainsKey(key));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetKeysAsync(
        string pattern = "*",
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyList<string> result;

        if (pattern == "*")
        {
            result = _store.Keys.ToList();
        }
        else
        {
            var regex = PatternToRegex(pattern);
            result = _store.Keys.Where(k => regex.IsMatch(k)).ToList();
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public IDisposable OnChange(string key, Action<string, object?> handler)
    {
        var handlers = _watchers.GetOrAdd(key, _ => new List<Action<string, object?>>());

        lock (_watcherLock)
        {
            handlers.Add(handler);
        }

        _logger.LogDebug("注册共享状态变更监听 {Key}", key);

        return new Subscription(() =>
        {
            lock (_watcherLock)
            {
                handlers.Remove(handler);
            }
            _logger.LogDebug("取消共享状态变更监听 {Key}", key);
        });
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _store.Clear();
        _logger.LogDebug("清除所有共享状态");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public int Count => _store.Count;

    /// <summary>
    /// 通知变更
    /// </summary>
    private void NotifyChange(string key, object? value)
    {
        if (_watchers.TryGetValue(key, out var handlers))
        {
            List<Action<string, object?>> handlersCopy;
            lock (_watcherLock)
            {
                handlersCopy = handlers.ToList();
            }

            foreach (var handler in handlersCopy)
            {
                try
                {
                    handler(key, value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理共享状态变更通知时出错 {Key}", key);
                }
            }
        }
    }

    /// <summary>
    /// 将通配符模式转换为正则表达式
    /// </summary>
    private static Regex PatternToRegex(string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return new Regex(regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// 订阅取消器
    /// </summary>
    private sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            if (!_disposed)
            {
                _unsubscribe();
                _disposed = true;
            }
        }
    }
}
