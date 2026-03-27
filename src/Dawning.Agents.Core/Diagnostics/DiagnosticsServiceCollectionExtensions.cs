using Dawning.Agents.Abstractions.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Diagnostics;

/// <summary>
/// Diagnostics services DI extension methods.
/// </summary>
public static class DiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// Adds diagnostics services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDiagnostics(this IServiceCollection services)
    {
        services.TryAddSingleton<IDiagnosticsProvider, DiagnosticsProvider>();
        services.TryAddSingleton<IPerformanceProfiler, PerformanceProfiler>();

        return services;
    }

    /// <summary>
    /// Adds diagnostics services with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="maxTraceCount">The maximum number of trace records.</param>
    /// <param name="slowOperationThreshold">The slow operation threshold.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDiagnostics(
        this IServiceCollection services,
        int maxTraceCount,
        TimeSpan? slowOperationThreshold = null
    )
    {
        services.TryAddSingleton<IDiagnosticsProvider, DiagnosticsProvider>();
        services.TryAddSingleton<IPerformanceProfiler>(sp => new PerformanceProfiler(
            logger: sp.GetService<Microsoft.Extensions.Logging.ILogger<PerformanceProfiler>>(),
            maxTraceCount: maxTraceCount,
            slowOperationThreshold: slowOperationThreshold
        ));

        return services;
    }
}
