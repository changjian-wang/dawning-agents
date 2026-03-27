namespace Dawning.Agents.Core.Evaluation;

using Dawning.Agents.Abstractions.Evaluation;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection extension methods for the evaluation framework.
/// </summary>
public static class EvaluationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the agent evaluation framework.
    /// </summary>
    public static IServiceCollection AddAgentEvaluation(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<EvaluationOptions>(
            configuration,
            EvaluationOptions.SectionName
        );

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMetricEvaluator, KeywordMatchEvaluator>()
        );
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMetricEvaluator, ToolCallAccuracyEvaluator>()
        );
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMetricEvaluator, LatencyEvaluator>()
        );
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMetricEvaluator, ExactMatchEvaluator>()
        );

        services.TryAddScoped<IAgentEvaluator, DefaultAgentEvaluator>();
        services.TryAddSingleton<ABTestRunner>();
        services.TryAddSingleton<EvaluationReportGenerator>();

        return services;
    }

    /// <summary>
    /// Registers the agent evaluation framework.
    /// </summary>
    public static IServiceCollection AddAgentEvaluation(
        this IServiceCollection services,
        Action<EvaluationOptions>? configureOptions = null
    )
    {
        if (configureOptions != null)
        {
            services.AddValidatedOptions(configureOptions);
        }
        else
        {
            services.AddValidatedOptions<EvaluationOptions>(_ => { });
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMetricEvaluator, KeywordMatchEvaluator>()
        );
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMetricEvaluator, ToolCallAccuracyEvaluator>()
        );
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMetricEvaluator, LatencyEvaluator>()
        );
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMetricEvaluator, ExactMatchEvaluator>()
        );

        services.TryAddScoped<IAgentEvaluator, DefaultAgentEvaluator>();
        services.TryAddSingleton<ABTestRunner>();
        services.TryAddSingleton<EvaluationReportGenerator>();

        return services;
    }

    /// <summary>
    /// Adds a custom metric evaluator.
    /// </summary>
    public static IServiceCollection AddMetricEvaluator<TEvaluator>(
        this IServiceCollection services
    )
        where TEvaluator : class, IMetricEvaluator
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMetricEvaluator, TEvaluator>());
        return services;
    }
}
