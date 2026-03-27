using Dawning.Agents.Abstractions;
using Dawning.Agents.Core.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Dawning.Agents.OpenTelemetry;

/// <summary>
/// Configuration options for OpenTelemetry.
/// </summary>
public sealed class OpenTelemetryOptions : IValidatableOptions
{
    public const string SectionName = "OpenTelemetry";

    /// <summary>
    /// Gets or sets a value indicating whether tracing is enabled.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether metrics collection is enabled.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the OTLP export endpoint (e.g. <c>http://localhost:4317</c>).
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the sampling ratio (0.0 - 1.0).
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the service name.
    /// </summary>
    public string ServiceName { get; set; } = AgentInstrumentation.ServiceName;

    /// <summary>
    /// Gets or sets the service version.
    /// </summary>
    public string ServiceVersion { get; set; } = AgentInstrumentation.ServiceVersion;

    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether console export is enabled (for development).
    /// </summary>
    public bool EnableConsoleExporter { get; set; } = false;

    /// <inheritdoc />
    public void Validate()
    {
        if (SamplingRatio is < 0.0 or > 1.0)
        {
            throw new InvalidOperationException("SamplingRatio must be between 0.0 and 1.0");
        }

        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            throw new InvalidOperationException("ServiceName is required");
        }

        if (
            !string.IsNullOrEmpty(OtlpEndpoint)
            && !Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out _)
        )
        {
            throw new InvalidOperationException("OtlpEndpoint must be a valid absolute URI");
        }
    }
}

/// <summary>
/// OpenTelemetry dependency injection extensions.
/// </summary>
public static class OpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing and metrics.
    /// </summary>
    public static IServiceCollection AddOpenTelemetryObservability(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var options =
            configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();

        options.Validate();

        services
            .AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName))
            .Validate(
                configured =>
                {
                    try
                    {
                        configured.Validate();
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                },
                $"Invalid {nameof(OpenTelemetryOptions)} configuration"
            )
            .ValidateOnStart();

        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion,
                serviceInstanceId: System.Environment.MachineName
            );

        if (!string.IsNullOrEmpty(options.Environment))
        {
            resourceBuilder.AddAttributes(
                new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", options.Environment),
                }
            );
        }

        var otelBuilder = services.AddOpenTelemetry();

        // Configure tracing
        if (options.EnableTracing)
        {
            otelBuilder.WithTracing(builder =>
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

        // Configure metrics
        if (options.EnableMetrics)
        {
            otelBuilder.WithMetrics(builder =>
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
    /// Registers tracing (simplified).
    /// </summary>
    public static IServiceCollection AddAgentTracing(
        this IServiceCollection services,
        string? otlpEndpoint = null,
        double samplingRatio = 1.0
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samplingRatio, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(samplingRatio, 1.0);

        services
            .AddOpenTelemetry()
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
    /// Registers metrics (simplified).
    /// </summary>
    public static IServiceCollection AddAgentMetrics(
        this IServiceCollection services,
        string? otlpEndpoint = null
    )
    {
        services
            .AddOpenTelemetry()
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
