using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Dawning.Agents.Core.Observability;

/// <summary>
/// OpenTelemetry 配置选项
/// </summary>
public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    /// <summary>
    /// 是否启用追踪
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// 是否启用指标
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// OTLP 导出端点 (如 http://localhost:4317)
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// 采样率 (0.0 - 1.0)
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = AgentInstrumentation.ServiceName;

    /// <summary>
    /// 服务版本
    /// </summary>
    public string ServiceVersion { get; set; } = AgentInstrumentation.ServiceVersion;

    /// <summary>
    /// 环境名称
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// 是否启用控制台导出 (开发环境)
    /// </summary>
    public bool EnableConsoleExporter { get; set; } = false;
}

/// <summary>
/// OpenTelemetry DI 扩展
/// </summary>
public static class OpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// 添加 OpenTelemetry 追踪和指标
    /// </summary>
    public static IServiceCollection AddOpenTelemetryObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
                      ?? new OpenTelemetryOptions();

        services.Configure<OpenTelemetryOptions>(configuration.GetSection(OpenTelemetryOptions.SectionName));

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion,
                serviceInstanceId: System.Environment.MachineName);

        if (!string.IsNullOrEmpty(options.Environment))
        {
            resourceBuilder.AddAttributes(new[]
            {
                new KeyValuePair<string, object>("deployment.environment", options.Environment),
            });
        }

        // 配置追踪
        if (options.EnableTracing)
        {
            services.AddOpenTelemetry()
                .WithTracing(builder =>
                {
                    builder
                        .SetResourceBuilder(resourceBuilder)
                        .AddSource(AgentInstrumentation.ServiceName)
                        .AddHttpClientInstrumentation()
                        .SetSampler(new TraceIdRatioBasedSampler(options.SamplingRatio));

                    if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                    {
                        builder.AddOtlpExporter(otlp =>
                        {
                            otlp.Endpoint = new Uri(options.OtlpEndpoint);
                        });
                    }

                    if (options.EnableConsoleExporter)
                    {
                        builder.AddConsoleExporter();
                    }
                });
        }

        // 配置指标
        if (options.EnableMetrics)
        {
            services.AddOpenTelemetry()
                .WithMetrics(builder =>
                {
                    builder
                        .SetResourceBuilder(resourceBuilder)
                        .AddMeter(AgentInstrumentation.ServiceName)
                        .AddRuntimeInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddPrometheusExporter();

                    if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                    {
                        builder.AddOtlpExporter(otlp =>
                        {
                            otlp.Endpoint = new Uri(options.OtlpEndpoint);
                        });
                    }

                    if (options.EnableConsoleExporter)
                    {
                        builder.AddConsoleExporter();
                    }
                });
        }

        return services;
    }

    /// <summary>
    /// 添加追踪 (简化版)
    /// </summary>
    public static IServiceCollection AddAgentTracing(
        this IServiceCollection services,
        string? otlpEndpoint = null,
        double samplingRatio = 1.0)
    {
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .AddSource(AgentInstrumentation.ServiceName)
                    .AddHttpClientInstrumentation()
                    .SetSampler(new TraceIdRatioBasedSampler(samplingRatio));

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    builder.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
                }
                else
                {
                    builder.AddConsoleExporter();
                }
            });

        return services;
    }

    /// <summary>
    /// 添加指标 (简化版)
    /// </summary>
    public static IServiceCollection AddAgentMetrics(
        this IServiceCollection services,
        string? otlpEndpoint = null)
    {
        services.AddOpenTelemetry()
            .WithMetrics(builder =>
            {
                builder
                    .AddMeter(AgentInstrumentation.ServiceName)
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    builder.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return services;
    }
}
