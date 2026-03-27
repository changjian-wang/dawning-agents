using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// Extension methods for registering safety guardrail services.
/// </summary>
public static class SafetyServiceCollectionExtensions
{
    /// <summary>
    /// Registers safety guardrail services using configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSafetyGuardrails(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<SafetyOptions>(configuration, SafetyOptions.SectionName);

        return services.AddSafetyGuardrailsCore();
    }

    /// <summary>
    /// Registers safety guardrail services with a configuration delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSafetyGuardrails(
        this IServiceCollection services,
        Action<SafetyOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.AddValidatedOptions(configure);
        }
        else
        {
            services.AddValidatedOptions<SafetyOptions>(_ => { });
        }

        return services.AddSafetyGuardrailsCore();
    }

    /// <summary>
    /// Registers the core guardrail services.
    /// </summary>
    private static IServiceCollection AddSafetyGuardrailsCore(this IServiceCollection services)
    {
        // Register the guardrail pipeline
        services.TryAddSingleton<IGuardrailPipeline>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SafetyOptions>>();
            var pipeline = new GuardrailPipeline(
                sp.GetService<Microsoft.Extensions.Logging.ILogger<GuardrailPipeline>>()
            );

            // Add default guardrails
            // Input guardrails
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

            if (options.Value.EnablePromptInjectionDetection)
            {
                pipeline.AddInputGuardrail(
                    new PromptInjectionGuardrail(
                        logger: sp.GetService<Microsoft.Extensions.Logging.ILogger<PromptInjectionGuardrail>>()
                    )
                );
            }

            // Output guardrails
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

        // Register individual guardrails for custom composition
        services.TryAddTransient<MaxLengthGuardrail>(sp =>
            MaxLengthGuardrail.ForInput(
                sp.GetRequiredService<IOptions<SafetyOptions>>(),
                sp.GetService<Microsoft.Extensions.Logging.ILogger<MaxLengthGuardrail>>()
            )
        );

        services.TryAddTransient<SensitiveDataGuardrail>();
        services.TryAddTransient<ContentFilterGuardrail>();
        services.TryAddTransient<UrlDomainGuardrail>();
        services.TryAddTransient<PromptInjectionGuardrail>();

        return services;
    }

    /// <summary>
    /// Adds a custom input guardrail.
    /// </summary>
    public static IServiceCollection AddInputGuardrail<T>(this IServiceCollection services)
        where T : class, IInputGuardrail
    {
        services.AddSingleton<IInputGuardrail, T>();
        return services;
    }

    /// <summary>
    /// Adds a custom output guardrail.
    /// </summary>
    public static IServiceCollection AddOutputGuardrail<T>(this IServiceCollection services)
        where T : class, IOutputGuardrail
    {
        services.AddSingleton<IOutputGuardrail, T>();
        return services;
    }

    /// <summary>
    /// Creates a custom guardrail pipeline.
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
    /// Registers the prompt injection detection guardrail independently.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPromptInjectionGuardrail(
        this IServiceCollection services,
        Action<PromptInjectionOptions>? configure = null
    )
    {
        var options = new PromptInjectionOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IInputGuardrail, PromptInjectionGuardrail>();

        return services;
    }

    /// <summary>
    /// Registers rate limiting services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRateLimiter(
        this IServiceCollection services,
        Action<RateLimitOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.AddValidatedOptions(configure);
        }
        else
        {
            services.AddValidatedOptions<RateLimitOptions>(_ => { });
        }

        services.TryAddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
        services.TryAddSingleton<ITokenRateLimiter, TokenRateLimiter>();

        return services;
    }

    /// <summary>
    /// Registers rate limiting services using configuration.
    /// </summary>
    public static IServiceCollection AddRateLimiter(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<RateLimitOptions>(configuration, RateLimitOptions.SectionName);

        services.TryAddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
        services.TryAddSingleton<ITokenRateLimiter, TokenRateLimiter>();

        return services;
    }

    /// <summary>
    /// Registers audit logging services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuditLogger(
        this IServiceCollection services,
        Action<AuditOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.AddValidatedOptions(configure);
        }
        else
        {
            services.AddValidatedOptions<AuditOptions>(_ => { });
        }

        services.TryAddSingleton<IAuditLogger, InMemoryAuditLogger>();

        return services;
    }

    /// <summary>
    /// Registers audit logging services using configuration.
    /// </summary>
    public static IServiceCollection AddAuditLogger(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<AuditOptions>(configuration, AuditOptions.SectionName);

        services.TryAddSingleton<IAuditLogger, InMemoryAuditLogger>();

        return services;
    }

    /// <summary>
    /// Registers file-based audit logging services (JSON Lines persistence).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFileAuditLogger(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<AuditOptions>(configuration, AuditOptions.SectionName);
        services.AddValidatedOptions<FileAuditOptions>(configuration, FileAuditOptions.SectionName);

        services.RemoveAll<IAuditLogger>();
        services.AddSingleton<IAuditLogger, FileAuditLogger>();

        return services;
    }

    /// <summary>
    /// Registers file-based audit logging services with a delegate.
    /// </summary>
    public static IServiceCollection AddFileAuditLogger(
        this IServiceCollection services,
        Action<FileAuditOptions>? configure = null
    )
    {
        services.AddValidatedOptions<AuditOptions>(_ => { });
        services.AddValidatedOptions(configure ?? (_ => { }));

        services.RemoveAll<IAuditLogger>();
        services.AddSingleton<IAuditLogger, FileAuditLogger>();

        return services;
    }

    /// <summary>
    /// Registers the complete safety infrastructure (guardrails + rate limiting + audit logging).
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
