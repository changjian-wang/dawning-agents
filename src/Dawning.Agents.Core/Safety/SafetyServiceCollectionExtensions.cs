using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 安全护栏服务注册扩展
/// </summary>
public static class SafetyServiceCollectionExtensions
{
    /// <summary>
    /// 添加安全护栏服务（从配置文件读取）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSafetyGuardrails(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<SafetyOptions>(configuration.GetSection(SafetyOptions.SectionName));

        return services.AddSafetyGuardrailsCore();
    }

    /// <summary>
    /// 添加安全护栏服务（带配置委托）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSafetyGuardrails(
        this IServiceCollection services,
        Action<SafetyOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.TryAddSingleton(Options.Create(new SafetyOptions()));
        }

        return services.AddSafetyGuardrailsCore();
    }

    /// <summary>
    /// 添加核心护栏服务
    /// </summary>
    private static IServiceCollection AddSafetyGuardrailsCore(this IServiceCollection services)
    {
        // 注册护栏管道
        services.TryAddSingleton<IGuardrailPipeline>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SafetyOptions>>();
            var pipeline = new GuardrailPipeline(
                sp.GetService<Microsoft.Extensions.Logging.ILogger<GuardrailPipeline>>()
            );

            // 添加默认护栏
            // 输入护栏
            pipeline.AddInputGuardrail(
                MaxLengthGuardrail.ForInput(
                    options,
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<MaxLengthGuardrail>>()
                )
            );

            if (options.Value.EnableSensitiveDataDetection)
            {
                pipeline.AddInputGuardrail(
                    new SensitiveDataGuardrail(
                        options,
                        sp.GetService<Microsoft.Extensions.Logging.ILogger<SensitiveDataGuardrail>>()
                    )
                );
            }

            if (options.Value.EnableContentFilter && options.Value.BlockedKeywords.Count > 0)
            {
                pipeline.AddInputGuardrail(
                    new ContentFilterGuardrail(
                        options,
                        sp.GetService<Microsoft.Extensions.Logging.ILogger<ContentFilterGuardrail>>()
                    )
                );
            }

            // 输出护栏
            pipeline.AddOutputGuardrail(
                MaxLengthGuardrail.ForOutput(
                    options,
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<MaxLengthGuardrail>>()
                )
            );

            if (options.Value.EnableSensitiveDataDetection)
            {
                pipeline.AddOutputGuardrail(
                    new SensitiveDataGuardrail(
                        options,
                        sp.GetService<Microsoft.Extensions.Logging.ILogger<SensitiveDataGuardrail>>()
                    )
                );
            }

            return pipeline;
        });

        // 单独注册各个护栏（供自定义组合使用）
        services.TryAddTransient<MaxLengthGuardrail>(sp =>
            MaxLengthGuardrail.ForInput(
                sp.GetRequiredService<IOptions<SafetyOptions>>(),
                sp.GetService<Microsoft.Extensions.Logging.ILogger<MaxLengthGuardrail>>()
            )
        );

        services.TryAddTransient<SensitiveDataGuardrail>();
        services.TryAddTransient<ContentFilterGuardrail>();
        services.TryAddTransient<UrlDomainGuardrail>();

        return services;
    }

    /// <summary>
    /// 添加自定义输入护栏
    /// </summary>
    public static IServiceCollection AddInputGuardrail<T>(this IServiceCollection services)
        where T : class, IInputGuardrail
    {
        services.AddSingleton<IInputGuardrail, T>();
        return services;
    }

    /// <summary>
    /// 添加自定义输出护栏
    /// </summary>
    public static IServiceCollection AddOutputGuardrail<T>(this IServiceCollection services)
        where T : class, IOutputGuardrail
    {
        services.AddSingleton<IOutputGuardrail, T>();
        return services;
    }

    /// <summary>
    /// 创建自定义护栏管道
    /// </summary>
    public static IServiceCollection AddCustomGuardrailPipeline(
        this IServiceCollection services,
        Action<IGuardrailPipeline, IServiceProvider> configure
    )
    {
        services.AddSingleton<IGuardrailPipeline>(sp =>
        {
            var pipeline = new GuardrailPipeline(
                sp.GetService<Microsoft.Extensions.Logging.ILogger<GuardrailPipeline>>()
            );
            configure(pipeline, sp);
            return pipeline;
        });

        return services;
    }

    /// <summary>
    /// 添加速率限制服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRateLimiter(
        this IServiceCollection services,
        Action<RateLimitOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.TryAddSingleton(Options.Create(new RateLimitOptions()));
        }

        services.TryAddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
        services.TryAddSingleton<TokenRateLimiter>();

        return services;
    }

    /// <summary>
    /// 添加速率限制服务（从配置文件读取）
    /// </summary>
    public static IServiceCollection AddRateLimiter(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<RateLimitOptions>(
            configuration.GetSection(RateLimitOptions.SectionName)
        );

        services.TryAddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
        services.TryAddSingleton<TokenRateLimiter>();

        return services;
    }

    /// <summary>
    /// 添加审计日志服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAuditLogger(
        this IServiceCollection services,
        Action<AuditOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.TryAddSingleton(Options.Create(new AuditOptions()));
        }

        services.TryAddSingleton<IAuditLogger, InMemoryAuditLogger>();

        return services;
    }

    /// <summary>
    /// 添加审计日志服务（从配置文件读取）
    /// </summary>
    public static IServiceCollection AddAuditLogger(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<AuditOptions>(configuration.GetSection(AuditOptions.SectionName));

        services.TryAddSingleton<IAuditLogger, InMemoryAuditLogger>();

        return services;
    }

    /// <summary>
    /// 添加完整的安全基础设施（护栏 + 速率限制 + 审计日志）
    /// </summary>
    public static IServiceCollection AddSafetyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSafetyGuardrails(configuration);
        services.AddRateLimiter(configuration);
        services.AddAuditLogger(configuration);

        return services;
    }
}
