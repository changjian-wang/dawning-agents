using Dawning.Agents.Abstractions.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Diagnostics;

/// <summary>
/// 诊断服务 DI 扩展方法
/// </summary>
public static class DiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// 添加诊断服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDiagnostics(this IServiceCollection services)
    {
        services.TryAddSingleton<IDiagnosticsProvider, DiagnosticsProvider>();
        services.TryAddSingleton<IPerformanceProfiler, PerformanceProfiler>();

        return services;
    }

    /// <summary>
    /// 添加诊断服务（带配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="maxTraceCount">最大追踪记录数</param>
    /// <param name="slowOperationThreshold">慢操作阈值</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDiagnostics(
        this IServiceCollection services,
        int maxTraceCount,
        TimeSpan? slowOperationThreshold = null
    )
    {
        services.TryAddSingleton<IDiagnosticsProvider, DiagnosticsProvider>();
        services.TryAddSingleton<IPerformanceProfiler>(sp =>
            new PerformanceProfiler(
                logger: sp.GetService<Microsoft.Extensions.Logging.ILogger<PerformanceProfiler>>(),
                maxTraceCount: maxTraceCount,
                slowOperationThreshold: slowOperationThreshold
            )
        );

        return services;
    }
}
