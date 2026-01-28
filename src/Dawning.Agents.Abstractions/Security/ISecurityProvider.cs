using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Dawning.Agents.Abstractions.Security;

/// <summary>
/// 认证结果
/// </summary>
public sealed record AuthenticationResult
{
    public bool IsAuthenticated { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyDictionary<string, string> Claims { get; init; } =
        new Dictionary<string, string>();
    public string? Error { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    public static AuthenticationResult Success(
        string userId,
        string? userName = null,
        IReadOnlyList<string>? roles = null,
        IReadOnlyDictionary<string, string>? claims = null,
        DateTimeOffset? expiresAt = null
    ) =>
        new()
        {
            IsAuthenticated = true,
            UserId = userId,
            UserName = userName,
            Roles = roles ?? [],
            Claims = claims ?? new Dictionary<string, string>(),
            ExpiresAt = expiresAt,
        };

    public static AuthenticationResult Failure(string error) =>
        new() { IsAuthenticated = false, Error = error };
}

/// <summary>
/// 授权结果
/// </summary>
public sealed record AuthorizationResult
{
    public bool IsAuthorized { get; init; }
    public string? Reason { get; init; }

    public static AuthorizationResult Allowed() => new() { IsAuthorized = true };

    public static AuthorizationResult Denied(string reason) =>
        new() { IsAuthorized = false, Reason = reason };
}

/// <summary>
/// 认证提供者接口
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// 验证令牌
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(
        string token,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 验证 API Key
    /// </summary>
    Task<AuthenticationResult> AuthenticateApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 授权提供者接口
/// </summary>
public interface IAuthorizationProvider
{
    /// <summary>
    /// 检查权限
    /// </summary>
    Task<AuthorizationResult> AuthorizeAsync(
        AuthenticationResult user,
        string resource,
        string action,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 检查工具执行权限
    /// </summary>
    Task<AuthorizationResult> AuthorizeToolAsync(
        AuthenticationResult user,
        string toolName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 检查 Agent 访问权限
    /// </summary>
    Task<AuthorizationResult> AuthorizeAgentAsync(
        AuthenticationResult user,
        string agentName,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 安全配置选项
/// </summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// 是否启用认证
    /// </summary>
    public bool EnableAuthentication { get; set; } = false;

    /// <summary>
    /// 是否启用授权
    /// </summary>
    public bool EnableAuthorization { get; set; } = false;

    /// <summary>
    /// 是否启用审计日志
    /// </summary>
    public bool EnableAuditLog { get; set; } = true;

    /// <summary>
    /// API Key 列表
    /// </summary>
    public Dictionary<string, ApiKeyConfig> ApiKeys { get; set; } = [];

    /// <summary>
    /// JWT 配置
    /// </summary>
    public JwtConfig? Jwt { get; set; }

    /// <summary>
    /// 角色权限映射
    /// </summary>
    public Dictionary<string, RolePermissions> Roles { get; set; } = [];
}

/// <summary>
/// API Key 配置
/// </summary>
public sealed class ApiKeyConfig
{
    public string Name { get; set; } = "";
    public IReadOnlyList<string> Roles { get; set; } = [];
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// JWT 配置
/// </summary>
public sealed class JwtConfig
{
    public string SecretKey { get; set; } = "";
    public string Issuer { get; set; } = "dawning-agents";
    public string Audience { get; set; } = "dawning-agents";
    public int ExpirationMinutes { get; set; } = 60;
}

/// <summary>
/// 角色权限
/// </summary>
public sealed class RolePermissions
{
    public IReadOnlyList<string> AllowedTools { get; set; } = [];
    public IReadOnlyList<string> AllowedAgents { get; set; } = [];
    public IReadOnlyList<string> DeniedTools { get; set; } = [];
    public int? MaxRequestsPerMinute { get; set; }
}
