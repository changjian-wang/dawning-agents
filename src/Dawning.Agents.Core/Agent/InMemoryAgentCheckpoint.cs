using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Agent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Agent;

/// <summary>
/// 基于内存的 Agent 检查点实现 — 适用于开发和测试场景
/// </summary>
public class InMemoryAgentCheckpoint : IAgentCheckpoint
{
    private readonly ConcurrentDictionary<string, AgentContext> _store = new();
    private readonly ILogger<InMemoryAgentCheckpoint> _logger;

    /// <summary>
    /// 初始化内存检查点
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public InMemoryAgentCheckpoint(ILogger<InMemoryAgentCheckpoint>? logger = null)
    {
        _logger = logger ?? NullLogger<InMemoryAgentCheckpoint>.Instance;
    }

    /// <inheritdoc />
    public Task SaveAsync(
        string sessionId,
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(context);

        _store[sessionId] = context;
        _logger.LogDebug("Checkpoint saved for session {SessionId}", sessionId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AgentContext?> LoadAsync(
        string sessionId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        _store.TryGetValue(sessionId, out var context);

        if (context is not null)
        {
            _logger.LogDebug("Checkpoint loaded for session {SessionId}", sessionId);
        }

        return Task.FromResult(context);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (_store.TryRemove(sessionId, out _))
        {
            _logger.LogDebug("Checkpoint deleted for session {SessionId}", sessionId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return Task.FromResult(_store.ContainsKey(sessionId));
    }
}
