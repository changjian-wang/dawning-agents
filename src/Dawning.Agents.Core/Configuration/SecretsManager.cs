namespace Dawning.Agents.Core.Configuration;

using Dawning.Agents.Abstractions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Environment-variable-based secrets manager.
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
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var envName = NormalizeEnvName(name);
        var value = Environment.GetEnvironmentVariable(envName);

        if (value != null)
        {
            _logger.LogDebug("Retrieved secret from environment variable: {Name}", name);
        }
        else
        {
            _logger.LogDebug("Environment variable not found: {Name} ({EnvName})", name, envName);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        var envName = NormalizeEnvName(name);
        Environment.SetEnvironmentVariable(envName, value);
        _logger.LogDebug("Set environment variable: {Name}", name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var envName = NormalizeEnvName(name);
        Environment.SetEnvironmentVariable(envName, null);
        _logger.LogDebug("Deleted environment variable: {Name}", name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var envName = NormalizeEnvName(name);
        var exists = Environment.GetEnvironmentVariable(envName) != null;
        return Task.FromResult(exists);
    }

    private static string NormalizeEnvName(string name)
    {
        // Convert common separators to underscores and uppercase
        return name.Replace("-", "_").Replace(":", "__").Replace(".", "_").ToUpperInvariant();
    }
}

/// <summary>
/// In-memory secrets manager (for testing and development).
/// </summary>
public class InMemorySecretsManager : ISecretsManager
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
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
        _logger.LogDebug("Set in-memory secret: {Name}", name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _secrets.Remove(name);
        }
        _logger.LogDebug("Deleted in-memory secret: {Name}", name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_lock)
        {
            return Task.FromResult(_secrets.ContainsKey(name));
        }
    }

    /// <summary>
    /// Clears all secrets.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _secrets.Clear();
        }
    }

    /// <summary>
    /// Gets the number of secrets.
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
/// Composite secrets manager (searches multiple sources in order).
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
        ArgumentNullException.ThrowIfNull(managers);
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
            var value = await manager.GetSecretAsync(name, cancellationToken).ConfigureAwait(false);
            if (value != null)
            {
                _logger.LogDebug(
                    "Retrieved secret from {ManagerType}: {Name}",
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
        // Set in the first manager
        if (_managers.Count > 0)
        {
            return _managers[0].SetSecretAsync(name, value, cancellationToken);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        // Delete from all managers
        foreach (var manager in _managers)
        {
            await manager.DeleteSecretAsync(name, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        foreach (var manager in _managers)
        {
            if (await manager.ExistsAsync(name, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }
        return false;
    }
}
