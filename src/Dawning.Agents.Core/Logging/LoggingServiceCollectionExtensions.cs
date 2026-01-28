using Dawning.Agents.Abstractions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Dawning.Agents.Core.Logging;

/// <summary>
/// Serilog 日志 DI 扩展方法
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Agent 结构化日志（Serilog）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAgentLogging(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var options = new LoggingOptions();
        configuration.GetSection(LoggingOptions.SectionName).Bind(options);

        return services.AddAgentLogging(options);
    }

    /// <summary>
    /// 添加 Agent 结构化日志（Serilog）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAgentLogging(
        this IServiceCollection services,
        Action<LoggingOptions> configure
    )
    {
        var options = new LoggingOptions();
        configure(options);

        return services.AddAgentLogging(options);
    }

    /// <summary>
    /// 添加 Agent 结构化日志（Serilog）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="options">配置选项</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAgentLogging(
        this IServiceCollection services,
        LoggingOptions options
    )
    {
        var loggerConfig = new LoggerConfiguration();

        // 最小级别
        var minLevel = ParseLogLevel(options.MinimumLevel);
        loggerConfig.MinimumLevel.Is(minLevel);

        // 命名空间级别覆盖
        foreach (var (ns, level) in options.Override)
        {
            loggerConfig.MinimumLevel.Override(ns, ParseLogLevel(level));
        }

        // Enrichers
        loggerConfig.Enrich.FromLogContext();
        loggerConfig.Enrich.With<AgentContextEnricher>();
        loggerConfig.Enrich.With<SpanIdEnricher>();

        if (options.EnrichWithMachineName)
        {
            loggerConfig.Enrich.WithMachineName();
        }

        if (options.EnrichWithThreadId)
        {
            loggerConfig.Enrich.WithThreadId();
        }

        // 控制台输出
        if (options.EnableConsole)
        {
            if (options.EnableJsonFormat)
            {
                loggerConfig.WriteTo.Console(new CompactJsonFormatter());
            }
            else
            {
                loggerConfig.WriteTo.Console(outputTemplate: options.OutputTemplate);
            }
        }

        // 文件输出
        if (options.EnableFile)
        {
            var rollingInterval = ConvertRollingInterval(options.RollingInterval);

            if (options.EnableJsonFormat)
            {
                loggerConfig.WriteTo.File(
                    new CompactJsonFormatter(),
                    options.FilePath,
                    rollingInterval: rollingInterval,
                    retainedFileCountLimit: options.RetainedFileCount
                );
            }
            else
            {
                loggerConfig.WriteTo.File(
                    options.FilePath,
                    rollingInterval: rollingInterval,
                    retainedFileCountLimit: options.RetainedFileCount,
                    outputTemplate: options.OutputTemplate
                );
            }
        }

        // 创建全局 Logger
        Log.Logger = loggerConfig.CreateLogger();

        // 替换 Microsoft.Extensions.Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        return services;
    }

    /// <summary>
    /// 添加默认的 Agent 日志配置（控制台输出）
    /// </summary>
    public static IServiceCollection AddAgentLogging(this IServiceCollection services)
    {
        return services.AddAgentLogging(new LoggingOptions());
    }

    private static LogEventLevel ParseLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "verbose" or "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" or "critical" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }

    private static Serilog.RollingInterval ConvertRollingInterval(RollingIntervalType interval)
    {
        return interval switch
        {
            RollingIntervalType.Infinite => Serilog.RollingInterval.Infinite,
            RollingIntervalType.Year => Serilog.RollingInterval.Year,
            RollingIntervalType.Month => Serilog.RollingInterval.Month,
            RollingIntervalType.Day => Serilog.RollingInterval.Day,
            RollingIntervalType.Hour => Serilog.RollingInterval.Hour,
            RollingIntervalType.Minute => Serilog.RollingInterval.Minute,
            _ => Serilog.RollingInterval.Day,
        };
    }
}
