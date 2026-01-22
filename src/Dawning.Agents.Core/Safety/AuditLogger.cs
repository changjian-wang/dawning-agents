using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 内存审计日志记录器
/// </summary>
public class InMemoryAuditLogger : IAuditLogger
{
    private readonly AuditOptions _options;
    private readonly ILogger<InMemoryAuditLogger> _logger;
    private readonly ConcurrentQueue<AuditEntry> _entries = new();
    private int _count;

    public InMemoryAuditLogger(
        IOptions<AuditOptions> options,
        ILogger<InMemoryAuditLogger>? logger = null
    )
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<InMemoryAuditLogger>.Instance;
    }

    /// <inheritdoc />
    public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        // 处理内容截断
        var processedEntry = ProcessEntry(entry);

        _entries.Enqueue(processedEntry);
        Interlocked.Increment(ref _count);

        // 清理过多的条目
        while (_count > _options.MaxInMemoryEntries && _entries.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
        }

        _logger.LogDebug(
            "审计日志: {EventType} - Session={SessionId}, Agent={AgentName}, Status={Status}",
            processedEntry.EventType,
            processedEntry.SessionId,
            processedEntry.AgentName,
            processedEntry.Status
        );

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditFilter filter,
        CancellationToken cancellationToken = default
    )
    {
        var query = _entries.AsEnumerable();

        if (!string.IsNullOrEmpty(filter.SessionId))
        {
            query = query.Where(e => e.SessionId == filter.SessionId);
        }

        if (!string.IsNullOrEmpty(filter.AgentName))
        {
            query = query.Where(e => e.AgentName == filter.AgentName);
        }

        if (filter.EventType.HasValue)
        {
            query = query.Where(e => e.EventType == filter.EventType.Value);
        }

        if (filter.StartTime.HasValue)
        {
            query = query.Where(e => e.Timestamp >= filter.StartTime.Value);
        }

        if (filter.EndTime.HasValue)
        {
            query = query.Where(e => e.Timestamp <= filter.EndTime.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(e => e.Status == filter.Status.Value);
        }

        var result = query.OrderByDescending(e => e.Timestamp).Take(filter.MaxResults).ToList();

        return Task.FromResult<IReadOnlyList<AuditEntry>>(result);
    }

    /// <summary>
    /// 获取所有条目（用于测试）
    /// </summary>
    public IReadOnlyList<AuditEntry> GetAllEntries() => [.. _entries];

    /// <summary>
    /// 清空所有条目
    /// </summary>
    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
        }
    }

    /// <summary>
    /// 当前条目数
    /// </summary>
    public int Count => _count;

    private AuditEntry ProcessEntry(AuditEntry entry)
    {
        var maxLen = _options.MaxContentLength;

        return entry with
        {
            Input = TruncateIfNeeded(entry.Input, maxLen, _options.LogInput),
            Output = TruncateIfNeeded(entry.Output, maxLen, _options.LogOutput),
            ToolArgs = TruncateIfNeeded(entry.ToolArgs, maxLen, _options.LogToolArgs),
        };
    }

    private static string? TruncateIfNeeded(string? content, int maxLength, bool shouldLog)
    {
        if (!shouldLog || string.IsNullOrEmpty(content))
        {
            return shouldLog ? content : "[REDACTED]";
        }

        if (content.Length <= maxLength)
        {
            return content;
        }

        return content[..maxLength] + "...[TRUNCATED]";
    }
}

/// <summary>
/// 审计日志扩展方法
/// </summary>
public static class AuditLoggerExtensions
{
    /// <summary>
    /// 记录 Agent 运行开始
    /// </summary>
    public static Task LogAgentRunStartAsync(
        this IAuditLogger logger,
        string agentName,
        string input,
        string? sessionId = null,
        CancellationToken cancellationToken = default
    )
    {
        return logger.LogAsync(
            new AuditEntry
            {
                EventType = AuditEventType.AgentRunStart,
                AgentName = agentName,
                Input = input,
                SessionId = sessionId,
            },
            cancellationToken
        );
    }

    /// <summary>
    /// 记录 Agent 运行结束
    /// </summary>
    public static Task LogAgentRunEndAsync(
        this IAuditLogger logger,
        string agentName,
        string? output,
        long durationMs,
        int? tokensUsed = null,
        AuditResultStatus status = AuditResultStatus.Success,
        string? errorMessage = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default
    )
    {
        return logger.LogAsync(
            new AuditEntry
            {
                EventType = AuditEventType.AgentRunEnd,
                AgentName = agentName,
                Output = output,
                DurationMs = durationMs,
                TokensUsed = tokensUsed,
                Status = status,
                ErrorMessage = errorMessage,
                SessionId = sessionId,
            },
            cancellationToken
        );
    }

    /// <summary>
    /// 记录工具调用
    /// </summary>
    public static Task LogToolCallAsync(
        this IAuditLogger logger,
        string toolName,
        string? toolArgs,
        string? output,
        long durationMs,
        AuditResultStatus status = AuditResultStatus.Success,
        string? errorMessage = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default
    )
    {
        return logger.LogAsync(
            new AuditEntry
            {
                EventType = AuditEventType.ToolCall,
                ToolName = toolName,
                ToolArgs = toolArgs,
                Output = output,
                DurationMs = durationMs,
                Status = status,
                ErrorMessage = errorMessage,
                SessionId = sessionId,
            },
            cancellationToken
        );
    }

    /// <summary>
    /// 记录护栏触发
    /// </summary>
    public static Task LogGuardrailTriggeredAsync(
        this IAuditLogger logger,
        string guardrailName,
        string? input,
        string reason,
        string? sessionId = null,
        CancellationToken cancellationToken = default
    )
    {
        return logger.LogAsync(
            new AuditEntry
            {
                EventType = AuditEventType.GuardrailTriggered,
                Input = input,
                ErrorMessage = reason,
                TriggeredGuardrails = [guardrailName],
                Status = AuditResultStatus.Blocked,
                SessionId = sessionId,
            },
            cancellationToken
        );
    }

    /// <summary>
    /// 记录速率限制
    /// </summary>
    public static Task LogRateLimitedAsync(
        this IAuditLogger logger,
        string key,
        TimeSpan retryAfter,
        string? sessionId = null,
        CancellationToken cancellationToken = default
    )
    {
        return logger.LogAsync(
            new AuditEntry
            {
                EventType = AuditEventType.RateLimited,
                ErrorMessage = $"Rate limited for {retryAfter.TotalSeconds:F1} seconds",
                Status = AuditResultStatus.RateLimited,
                SessionId = sessionId,
                Metadata = new Dictionary<string, object>
                {
                    ["key"] = key,
                    ["retryAfterSeconds"] = retryAfter.TotalSeconds,
                },
            },
            cancellationToken
        );
    }
}
