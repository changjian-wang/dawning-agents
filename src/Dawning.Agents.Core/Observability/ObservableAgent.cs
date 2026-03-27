namespace Dawning.Agents.Core.Observability;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Wraps an agent with full observability support.
/// </summary>
public sealed class ObservableAgent : IAgent, IDisposable
{
    private readonly IAgent _innerAgent;
    private readonly AgentTelemetry _telemetry;
    private readonly AgentLogger _agentLogger;
    private readonly DistributedTracer _tracer;
    private readonly MetricsCollector _metrics;
    private readonly Dictionary<string, string> _agentTag;
    private readonly Dictionary<string, string> _agentSuccessTag;
    private readonly Dictionary<string, string> _agentFailureTag;
    private volatile bool _disposed;

    /// <inheritdoc />
    public string Name => $"Observable({_innerAgent.Name})";

    /// <inheritdoc />
    public string Instructions => _innerAgent.Instructions;

    /// <summary>
    /// Gets the inner agent.
    /// </summary>
    public IAgent InnerAgent => _innerAgent;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableAgent"/> class.
    /// </summary>
    public ObservableAgent(
        IAgent innerAgent,
        AgentTelemetry telemetry,
        TelemetryConfig config,
        ILogger? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(innerAgent);
        ArgumentNullException.ThrowIfNull(telemetry);
        ArgumentNullException.ThrowIfNull(config);
        _innerAgent = innerAgent;
        _telemetry = telemetry;
        _agentLogger = new AgentLogger(logger ?? NullLogger.Instance, innerAgent.Name, config);
        _tracer = new DistributedTracer(config);
        _metrics = new MetricsCollector();
        _agentTag = new Dictionary<string, string> { ["agent"] = innerAgent.Name };
        _agentSuccessTag = new Dictionary<string, string>
        {
            ["agent"] = innerAgent.Name,
            ["success"] = "true",
        };
        _agentFailureTag = new Dictionary<string, string>
        {
            ["agent"] = innerAgent.Name,
            ["success"] = "false",
        };
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var context = new AgentContext { UserInput = input };
        return await RunAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var requestId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTimeOffset.UtcNow;

        // Start trace span
        using var span = _tracer.StartSpan($"{_innerAgent.Name}.Run", SpanKind.Internal);
        span.SetAttribute("request.id", requestId);
        span.SetAttribute("input.length", context.UserInput.Length);

        // Set log context
        using var logContext = LogContext
            .Push()
            .WithRequestId(requestId)
            .WithAgentName(_innerAgent.Name)
            .WithSessionId(context.SessionId);

        // Track active requests
        using var activeTracker = _telemetry.TrackActiveRequest(_innerAgent.Name);

        _agentLogger.LogRequestStart(requestId, context.UserInput);

        try
        {
            var response = await _innerAgent
                .RunAsync(context, cancellationToken)
                .ConfigureAwait(false);
            var duration = DateTimeOffset.UtcNow - startTime;

            // Record success metrics
            _telemetry.RecordRequest(
                _innerAgent.Name,
                response.Success,
                duration.TotalMilliseconds
            );

            _metrics.IncrementCounter(
                "agent.requests",
                1,
                response.Success ? _agentSuccessTag : _agentFailureTag
            );

            _metrics.RecordHistogram("agent.latency_ms", duration.TotalMilliseconds, _agentTag);

            _agentLogger.LogRequestComplete(requestId, response.Success, duration);

            span.SetAttribute("response.success", response.Success);
            span.SetAttribute("response.length", response.FinalAnswer?.Length ?? 0);
            span.SetStatus(response.Success ? SpanStatus.Ok : SpanStatus.Error);

            return response;
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;

            _telemetry.RecordRequest(_innerAgent.Name, false, duration.TotalMilliseconds);
            _metrics.IncrementCounter(
                "agent.errors",
                1,
                new Dictionary<string, string>
                {
                    ["agent"] = _innerAgent.Name,
                    ["error_type"] = ex.GetType().Name,
                }
            );

            _agentLogger.LogError(requestId, ex, "Agent execution failed");

            span.RecordException(ex);
            span.SetStatus(SpanStatus.Error, ex.Message);

            throw;
        }
    }

    /// <summary>
    /// Gets the current metrics snapshot.
    /// </summary>
    public MetricsSnapshot GetMetrics() => _metrics.GetSnapshot();

    /// <summary>
    /// Gets the metrics collector.
    /// </summary>
    public MetricsCollector MetricsCollector => _metrics;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tracer.Dispose();
        // _telemetry is a shared singleton managed by the DI container; do not dispose here
        (_innerAgent as IDisposable)?.Dispose();
    }
}
