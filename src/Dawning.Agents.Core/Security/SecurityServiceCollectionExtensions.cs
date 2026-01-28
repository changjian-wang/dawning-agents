using Dawning.Agents.Abstractions.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Security;

/// <summary>
/// 安全服务 DI 扩展
/// </summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>
    /// 添加安全服务
    /// </summary>
    public static IServiceCollection AddAgentSecurity(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.Configure<RateLimitOptions>(
            configuration.GetSection(RateLimitOptions.SectionName)
        );

        // 认证提供者
        services.TryAddSingleton<IAuthenticationProvider, ApiKeyAuthenticationProvider>();

        // 授权提供者
        services.TryAddSingleton<IAuthorizationProvider, RoleBasedAuthorizationProvider>();

        // 审计日志
        services.TryAddSingleton<IAuditLogProvider, InMemoryAuditLogProvider>();

        // 速率限制
        services.TryAddSingleton<IRateLimiter, SlidingWindowRateLimiter>();

        return services;
    }

    /// <summary>
    /// 添加 API Key 认证
    /// </summary>
    public static IServiceCollection AddApiKeyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.TryAddSingleton<IAuthenticationProvider, ApiKeyAuthenticationProvider>();
        return services;
    }

    /// <summary>
    /// 添加角色授权
    /// </summary>
    public static IServiceCollection AddRoleBasedAuthorization(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.TryAddSingleton<IAuthorizationProvider, RoleBasedAuthorizationProvider>();
        return services;
    }

    /// <summary>
    /// 添加审计日志
    /// </summary>
    public static IServiceCollection AddAuditLogging(this IServiceCollection services)
    {
        services.TryAddSingleton<IAuditLogProvider, InMemoryAuditLogProvider>();
        return services;
    }

    /// <summary>
    /// 添加速率限制
    /// </summary>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<RateLimitOptions>(
            configuration.GetSection(RateLimitOptions.SectionName)
        );
        services.TryAddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
        return services;
    }

    /// <summary>
    /// 添加速率限制（简化版）
    /// </summary>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        int requestsPerMinute = 60
    )
    {
        services.Configure<RateLimitOptions>(opts =>
        {
            opts.Enabled = true;
            opts.DefaultRequestsPerMinute = requestsPerMinute;
        });
        services.TryAddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
        return services;
    }
}
