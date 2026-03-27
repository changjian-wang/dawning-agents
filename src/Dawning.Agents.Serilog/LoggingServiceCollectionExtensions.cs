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

namespace Dawning.Agents.Serilog;

/// <summary>
/// Serilog logging dependency injection extension methods.
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    /// <summary>
    /// Registers agent structured logging with Serilog.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
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
    /// Registers agent structured logging with Serilog.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
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
    /// Registers agent structured logging with Serilog.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The logging options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentLogging(
        this IServiceCollection services,
        LoggingOptions options
    )
    {
        var loggerConfig = new LoggerConfiguration();

        // Minimum level (controlled via LevelSwitch, supports runtime dynamic adjustment)
        var minLevel = ParseLogLevel(options.MinimumLevel);
        var levelSwitch = new LoggingLevelSwitch(minLevel);
        loggerConfig.MinimumLevel.ControlledBy(levelSwitch);

        // Namespace-level overrides
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

        // Console output
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

        // File output
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

        // Elasticsearch output
        if (options.Elasticsearch?.Enabled == true)
        {
            ConfigureElasticsearchSink(loggerConfig, options.Elasticsearch);
        }

        // Seq output (recommended for development)
        if (options.Seq?.Enabled == true)
        {
            ConfigureSeqSink(loggerConfig, options.Seq);
        }

        // Create global logger
        Log.Logger = loggerConfig.CreateLogger();

        // Register log level switch (for dynamic adjustment)
        services.AddSingleton(levelSwitch);
        services.AddSingleton<ILogLevelController>(sp => new LogLevelController(
            sp.GetRequiredService<LoggingLevelSwitch>()
        ));

        // Replace Microsoft.Extensions.Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        return services;
    }

    /// <summary>
    /// Configures the Elasticsearch sink.
    /// </summary>
    private static void ConfigureElasticsearchSink(
        LoggerConfiguration loggerConfig,
        ElasticsearchLoggingOptions esOptions
    )
    {
        var nodes = esOptions.NodeUris.Select(uri => new Uri(uri)).ToArray();

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
    /// Configures the Seq sink.
    /// </summary>
    private static void ConfigureSeqSink(
        LoggerConfiguration loggerConfig,
        SeqLoggingOptions seqOptions
    )
    {
        loggerConfig.WriteTo.Seq(
            seqOptions.ServerUrl,
            apiKey: seqOptions.ApiKey,
            period: TimeSpan.FromSeconds(seqOptions.BatchIntervalSeconds)
        );
    }

    /// <summary>
    /// Registers agent logging with default settings (console output).
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

    private static global::Serilog.RollingInterval ConvertRollingInterval(
        RollingIntervalType interval
    )
    {
        return interval switch
        {
            RollingIntervalType.Infinite => global::Serilog.RollingInterval.Infinite,
            RollingIntervalType.Year => global::Serilog.RollingInterval.Year,
            RollingIntervalType.Month => global::Serilog.RollingInterval.Month,
            RollingIntervalType.Day => global::Serilog.RollingInterval.Day,
            RollingIntervalType.Hour => global::Serilog.RollingInterval.Hour,
            RollingIntervalType.Minute => global::Serilog.RollingInterval.Minute,
            _ => global::Serilog.RollingInterval.Day,
        };
    }
}
