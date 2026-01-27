using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dawning.Agents.Abstractions.Security;

/// <summary>
/// 审计日志条目
/// </summary>
public sealed record AuditLogEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string Action { get; init; } = "";
    public string Resource { get; init; } = "";
    public string? ResourceId { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public TimeSpan? Duration { get; init; }
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// 审计日志操作类型
/// </summary>
public static class AuditActions
{
    public const string AgentRequest = "agent.request";
    public const string AgentResponse = "agent.response";
    public const string ToolExecute = "tool.execute";
    public const string LLMCall = "llm.call";
    public const string Authentication = "auth.authenticate";
    public const string AuthenticationFailed = "auth.authenticate.failed";
    public const string Authorization = "auth.authorize";
    public const string AuthorizationDenied = "auth.authorize.denied";
    public const string RateLimitExceeded = "ratelimit.exceeded";
    public const string ConfigChange = "config.change";
}

/// <summary>
/// 审计日志提供者接口
/// </summary>
public interface IAuditLogProvider
{
    /// <summary>
    /// 写入审计日志
    /// </summary>
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量写入审计日志
    /// </summary>
    Task WriteBatchAsync(IEnumerable<AuditLogEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询审计日志
    /// </summary>
    Task<IReadOnlyList<AuditLogEntry>> QueryAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 审计日志查询
/// </summary>
public sealed class AuditLogQuery
{
    public string? UserId { get; set; }
    public string? Action { get; set; }
    public string? Resource { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public bool? IsSuccess { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 100;
}
