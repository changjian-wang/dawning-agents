using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dawning.Agents.Abstractions.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Security;

/// <summary>
/// API Key 认证提供者
/// </summary>
public sealed class ApiKeyAuthenticationProvider : IAuthenticationProvider
{
    private readonly SecurityOptions _options;
    private readonly ILogger<ApiKeyAuthenticationProvider> _logger;

    public ApiKeyAuthenticationProvider(
        IOptions<SecurityOptions> options,
        ILogger<ApiKeyAuthenticationProvider>? logger = null)
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<ApiKeyAuthenticationProvider>.Instance;
    }

    public Task<AuthenticationResult> AuthenticateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        // Bearer token 认证 (简化实现，实际应验证 JWT)
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticationResult.Failure("Token is required"));
        }

        // 这里应该实现 JWT 验证逻辑
        _logger.LogDebug("Token 认证: {TokenPrefix}...", token[..Math.Min(10, token.Length)]);
        return Task.FromResult(AuthenticationResult.Failure("JWT authentication not implemented"));
    }

    public Task<AuthenticationResult> AuthenticateApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return Task.FromResult(AuthenticationResult.Failure("API key is required"));
        }

        if (_options.ApiKeys.TryGetValue(apiKey, out var config))
        {
            if (!config.IsEnabled)
            {
                _logger.LogWarning("API Key 已禁用: {Name}", config.Name);
                return Task.FromResult(AuthenticationResult.Failure("API key is disabled"));
            }

            if (config.ExpiresAt.HasValue && config.ExpiresAt.Value < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("API Key 已过期: {Name}", config.Name);
                return Task.FromResult(AuthenticationResult.Failure("API key has expired"));
            }

            _logger.LogDebug("API Key 认证成功: {Name}", config.Name);
            return Task.FromResult(AuthenticationResult.Success(
                userId: apiKey,
                userName: config.Name,
                roles: config.Roles.ToList(),
                expiresAt: config.ExpiresAt));
        }

        _logger.LogWarning("无效的 API Key");
        return Task.FromResult(AuthenticationResult.Failure("Invalid API key"));
    }
}

/// <summary>
/// 基于角色的授权提供者
/// </summary>
public sealed class RoleBasedAuthorizationProvider : IAuthorizationProvider
{
    private readonly SecurityOptions _options;
    private readonly ILogger<RoleBasedAuthorizationProvider> _logger;

    public RoleBasedAuthorizationProvider(
        IOptions<SecurityOptions> options,
        ILogger<RoleBasedAuthorizationProvider>? logger = null)
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<RoleBasedAuthorizationProvider>.Instance;
    }

    public Task<AuthorizationResult> AuthorizeAsync(
        AuthenticationResult user,
        string resource,
        string action,
        CancellationToken cancellationToken = default)
    {
        if (!user.IsAuthenticated)
        {
            return Task.FromResult(AuthorizationResult.Denied("User is not authenticated"));
        }

        // 管理员角色拥有所有权限
        if (user.Roles.Contains("admin"))
        {
            return Task.FromResult(AuthorizationResult.Allowed());
        }

        _logger.LogDebug("授权检查: User={UserId}, Resource={Resource}, Action={Action}",
            user.UserId, resource, action);

        return Task.FromResult(AuthorizationResult.Allowed());
    }

    public Task<AuthorizationResult> AuthorizeToolAsync(
        AuthenticationResult user,
        string toolName,
        CancellationToken cancellationToken = default)
    {
        if (!user.IsAuthenticated)
        {
            return Task.FromResult(AuthorizationResult.Denied("User is not authenticated"));
        }

        foreach (var role in user.Roles)
        {
            if (_options.Roles.TryGetValue(role, out var permissions))
            {
                // 检查是否在拒绝列表中
                if (permissions.DeniedTools.Contains(toolName) ||
                    permissions.DeniedTools.Contains("*"))
                {
                    _logger.LogWarning("工具访问被拒绝: User={UserId}, Tool={Tool}, Role={Role}",
                        user.UserId, toolName, role);
                    return Task.FromResult(AuthorizationResult.Denied($"Tool '{toolName}' is denied for role '{role}'"));
                }

                // 检查是否在允许列表中
                if (permissions.AllowedTools.Contains(toolName) ||
                    permissions.AllowedTools.Contains("*"))
                {
                    return Task.FromResult(AuthorizationResult.Allowed());
                }
            }
        }

        // 默认允许（如果没有配置限制）
        return Task.FromResult(AuthorizationResult.Allowed());
    }

    public Task<AuthorizationResult> AuthorizeAgentAsync(
        AuthenticationResult user,
        string agentName,
        CancellationToken cancellationToken = default)
    {
        if (!user.IsAuthenticated)
        {
            return Task.FromResult(AuthorizationResult.Denied("User is not authenticated"));
        }

        foreach (var role in user.Roles)
        {
            if (_options.Roles.TryGetValue(role, out var permissions))
            {
                if (permissions.AllowedAgents.Contains(agentName) ||
                    permissions.AllowedAgents.Contains("*"))
                {
                    return Task.FromResult(AuthorizationResult.Allowed());
                }
            }
        }

        // 默认允许
        return Task.FromResult(AuthorizationResult.Allowed());
    }
}

/// <summary>
/// 内存审计日志提供者
/// </summary>
public sealed class InMemoryAuditLogProvider : IAuditLogProvider
{
    private readonly ConcurrentQueue<AuditLogEntry> _entries = new();
    private readonly ILogger<InMemoryAuditLogProvider> _logger;
    private readonly int _maxEntries;

    public InMemoryAuditLogProvider(
        ILogger<InMemoryAuditLogProvider>? logger = null,
        int maxEntries = 10000)
    {
        _logger = logger ?? NullLogger<InMemoryAuditLogProvider>.Instance;
        _maxEntries = maxEntries;
    }

    public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Enqueue(entry);

        // 限制队列大小
        while (_entries.Count > _maxEntries)
        {
            _entries.TryDequeue(out _);
        }

        _logger.LogDebug("审计日志: {Action} {Resource} by {UserId} - {IsSuccess}",
            entry.Action, entry.Resource, entry.UserId, entry.IsSuccess);

        return Task.CompletedTask;
    }

    public Task WriteBatchAsync(IEnumerable<AuditLogEntry> entries, CancellationToken cancellationToken = default)
    {
        foreach (var entry in entries)
        {
            _entries.Enqueue(entry);
        }

        while (_entries.Count > _maxEntries)
        {
            _entries.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditLogEntry>> QueryAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = _entries.AsEnumerable();

        if (!string.IsNullOrEmpty(query.UserId))
        {
            results = results.Where(e => e.UserId == query.UserId);
        }

        if (!string.IsNullOrEmpty(query.Action))
        {
            results = results.Where(e => e.Action == query.Action);
        }

        if (!string.IsNullOrEmpty(query.Resource))
        {
            results = results.Where(e => e.Resource == query.Resource);
        }

        if (query.StartTime.HasValue)
        {
            results = results.Where(e => e.Timestamp >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            results = results.Where(e => e.Timestamp <= query.EndTime.Value);
        }

        if (query.IsSuccess.HasValue)
        {
            results = results.Where(e => e.IsSuccess == query.IsSuccess.Value);
        }

        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(
            results
                .OrderByDescending(e => e.Timestamp)
                .Skip(query.Skip)
                .Take(query.Take)
                .ToList());
    }
}
