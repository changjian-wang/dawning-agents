using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// 工具会话实现 — 管理 session 级别的动态工具，聚合多层级工具解析
/// </summary>
public sealed class ToolSession : IToolSession
{
    private readonly IToolSandbox _sandbox;
    private readonly IToolStore _store;
    private readonly ToolSandboxOptions _defaultOptions;
    private readonly ILogger<ToolSession> _logger;
    private readonly ConcurrentDictionary<string, EphemeralTool> _sessionTools = new(
        StringComparer.OrdinalIgnoreCase
    );

    private volatile bool _disposed;

    /// <summary>
    /// 创建工具会话
    /// </summary>
    public ToolSession(
        IToolSandbox sandbox,
        IToolStore store,
        ToolSandboxOptions? defaultOptions = null,
        ILogger<ToolSession>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(store);
        _sandbox = sandbox;
        _store = store;
        _defaultOptions = defaultOptions ?? new ToolSandboxOptions();
        _logger = logger ?? NullLogger<ToolSession>.Instance;
    }

    /// <inheritdoc />
    public ITool CreateTool(EphemeralToolDefinition definition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new ArgumentException("Tool name cannot be empty", nameof(definition));
        }

        var tool = new EphemeralTool(definition, _sandbox, _defaultOptions, _logger);

        if (!_sessionTools.TryAdd(definition.Name, tool))
        {
            // Replace existing tool with same name
            _sessionTools[definition.Name] = tool;
            _logger.LogDebug("Replaced existing session tool: {Name}", definition.Name);
        }
        else
        {
            _logger.LogDebug("Created session tool: {Name}", definition.Name);
        }

        return tool;
    }

    /// <inheritdoc />
    public IReadOnlyList<ITool> GetSessionTools()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _sessionTools.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task PromoteToolAsync(
        string name,
        ToolScope targetScope,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (targetScope == ToolScope.Session)
        {
            throw new ArgumentException(
                "Cannot promote to Session scope (already in session)",
                nameof(targetScope)
            );
        }

        if (!_sessionTools.TryGetValue(name, out var tool))
        {
            throw new InvalidOperationException($"Tool '{name}' not found in current session");
        }

        _logger.LogInformation("Promoting tool '{Name}' to {Scope}", name, targetScope);

        var definition = tool.Definition;
        definition.Scope = targetScope;

        await _store
            .SaveToolAsync(definition, targetScope, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveToolAsync(
        string name,
        ToolScope scope,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (scope == ToolScope.Session)
        {
            _sessionTools.TryRemove(name, out _);
            _logger.LogDebug("Removed session tool: {Name}", name);
        }
        else
        {
            await _store.DeleteToolAsync(name, scope, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Removed {Scope} tool: {Name}", scope, name);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EphemeralToolDefinition>> ListToolsAsync(
        ToolScope scope,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (scope == ToolScope.Session)
        {
            return _sessionTools.Values.Select(t => t.Definition).ToList().AsReadOnly();
        }

        return await _store.LoadToolsAsync(scope, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sessionTools.Clear();
        _logger.LogDebug("Tool session disposed");
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
