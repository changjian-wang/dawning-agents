using Dawning.Agents.Abstractions.Logging;
using Elastic.Channels;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
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

        // Elasticsearch 输出
        if (options.Elasticsearch?.Enabled == true)
        {
            ConfigureElasticsearchSink(loggerConfig, options.Elasticsearch);
        }

        // Seq 输出（开发环境推荐）
        if (options.Seq?.Enabled == true)
        {
            ConfigureSeqSink(loggerConfig, options.Seq);
        }

        // 创建全局 Logger
        Log.Logger = loggerConfig.CreateLogger();

        // 注册日志级别开关（用于动态调整）
        var levelSwitch = new LoggingLevelSwitch(minLevel);
        services.AddSingleton(levelSwitch);
        services.AddSingleton<ILogLevelController>(sp =>
            new LogLevelController(sp.GetRequiredService<LoggingLevelSwitch>())
        );

        // 替换 Microsoft.Extensions.Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        return services;
    }

    /// <summary>
    /// 配置 Elasticsearch Sink
    /// </summary>
    private static void ConfigureElasticsearchSink(
        LoggerConfiguration loggerConfig,
        ElasticsearchLoggingOptions esOptions
    )
    {
        var nodes = esOptions.NodeUris.Select(uri => new Uri(uri)).ToArray();

        // 配置传输
        TransportConfiguration transportConfig;

        if (!string.IsNullOrEmpty(esOptions.ApiKey))
        {
            transportConfig = new TransportConfiguration(new Uri(esOptions.NodeUris[0]))
                .Authentication(new ApiKey(esOptions.ApiKey));
        }
        else if (
            !string.IsNullOrEmpty(esOptions.Username) && !string.IsNullOrEmpty(esOptions.Password)
        )
        {
            transportConfig = new TransportConfiguration(new Uri(esOptions.NodeUris[0]))
                .Authentication(new BasicAuthentication(esOptions.Username, esOptions.Password));
        }
        else
        {
            transportConfig = new TransportConfiguration(new Uri(esOptions.NodeUris[0]));
        }

        loggerConfig.WriteTo.Elasticsearch(
            nodes,
            opts =>
            {
                opts.DataStream = new DataStreamName("logs", "dawning-agents");
                opts.BootstrapMethod = BootstrapMethod.Failure;
            },
            transport =>
            {
                if (!string.IsNullOrEmpty(esOptions.ApiKey))
                {
                    transport.Authentication(new ApiKey(esOptions.ApiKey));
                }
                else if (
                    !string.IsNullOrEmpty(esOptions.Username)
                    && !string.IsNullOrEmpty(esOptions.Password)
                )
                {
                    transport.Authentication(
                        new BasicAuthentication(esOptions.Username, esOptions.Password)
                    );
                }
            }
        );
    }

    /// <summary>
    /// 配置 Seq Sink
    /// </summary>
    private static void ConfigureSeqSink(LoggerConfiguration loggerConfig, SeqLoggingOptions seqOptions)
    {
        loggerConfig.WriteTo.Seq(
            seqOptions.ServerUrl,
            apiKey: seqOptions.ApiKey,
            period: TimeSpan.FromSeconds(seqOptions.BatchIntervalSeconds)
        );
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
