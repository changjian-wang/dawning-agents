namespace Dawning.Agents.Core.Configuration;

using Dawning.Agents.Abstractions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// 基于环境变量的密钥管理器
/// </summary>
public class EnvironmentSecretsManager : ISecretsManager
{
    private readonly ILogger<EnvironmentSecretsManager> _logger;

    public EnvironmentSecretsManager(ILogger<EnvironmentSecretsManager>? logger = null)
    {
        _logger = logger ?? NullLogger<EnvironmentSecretsManager>.Instance;
    }

    /// <inheritdoc />
    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var envName = NormalizeEnvName(name);
        var value = Environment.GetEnvironmentVariable(envName);

        if (value != null)
        {
            _logger.LogDebug("已从环境变量获取密钥: {Name}", name);
        }
        else
        {
            _logger.LogDebug("环境变量不存在: {Name} ({EnvName})", name, envName);
        }

        return Task.FromResult(value);
    }

    /// <inheritdoc />
    public Task SetSecretAsync(
        string name,
        string value,
        CancellationToken cancellationToken = default
    )
    {
        var envName = NormalizeEnvName(name);
        Environment.SetEnvironmentVariable(envName, value);
        _logger.LogDebug("已设置环境变量: {Name}", name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var envName = NormalizeEnvName(name);
        Environment.SetEnvironmentVariable(envName, null);
        _logger.LogDebug("已删除环境变量: {Name}", name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        var envName = NormalizeEnvName(name);
        var exists = Environment.GetEnvironmentVariable(envName) != null;
        return Task.FromResult(exists);
    }

    private static string NormalizeEnvName(string name)
    {
        // 将常见分隔符转换为下划线，转大写
        return name.Replace("-", "_").Replace(":", "__").Replace(".", "_").ToUpperInvariant();
    }
}

/// <summary>
/// 内存密钥管理器（用于测试和开发）
/// </summary>
public class InMemorySecretsManager : ISecretsManager
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private readonly ILogger<InMemorySecretsManager> _logger;

    public InMemorySecretsManager(ILogger<InMemorySecretsManager>? logger = null)
    {
        _logger = logger ?? NullLogger<InMemorySecretsManager>.Instance;
    }

    /// <inheritdoc />
    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _secrets.TryGetValue(name, out var value);
            return Task.FromResult(value);
        }
    }

    /// <inheritdoc />
    public Task SetSecretAsync(
        string name,
        string value,
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            _secrets[name] = value;
        }
        _logger.LogDebug("已设置内存密钥: {Name}", name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _secrets.Remove(name);
        }
        _logger.LogDebug("已删除内存密钥: {Name}", name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_secrets.ContainsKey(name));
        }
    }

    /// <summary>
    /// 清除所有密钥
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _secrets.Clear();
        }
    }

    /// <summary>
    /// 获取密钥数量
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _secrets.Count;
            }
        }
    }
}

/// <summary>
/// 组合密钥管理器（按顺序查找多个来源）
/// </summary>
public class CompositeSecretsManager : ISecretsManager
{
    private readonly IReadOnlyList<ISecretsManager> _managers;
    private readonly ILogger<CompositeSecretsManager> _logger;

    public CompositeSecretsManager(
        IEnumerable<ISecretsManager> managers,
        ILogger<CompositeSecretsManager>? logger = null
    )
    {
        _managers = managers.ToList();
        _logger = logger ?? NullLogger<CompositeSecretsManager>.Instance;
    }

    /// <inheritdoc />
    public async Task<string?> GetSecretAsync(
        string name,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var manager in _managers)
        {
            var value = await manager.GetSecretAsync(name, cancellationToken);
            if (value != null)
            {
                _logger.LogDebug(
                    "从 {ManagerType} 获取到密钥: {Name}",
                    manager.GetType().Name,
                    name
                );
                return value;
            }
        }
        return null;
    }

    /// <inheritdoc />
    public Task SetSecretAsync(
        string name,
        string value,
        CancellationToken cancellationToken = default
    )
    {
        // 设置到第一个管理器
        if (_managers.Count > 0)
        {
            return _managers[0].SetSecretAsync(name, value, cancellationToken);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        // 从所有管理器删除
        foreach (var manager in _managers)
        {
            await manager.DeleteSecretAsync(name, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        foreach (var manager in _managers)
        {
            if (await manager.ExistsAsync(name, cancellationToken))
            {
                return true;
            }
        }
        return false;
    }
}
