namespace Dawning.Agents.Core.Observability;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dawning.Agents.Abstractions.Observability;

/// <summary>
/// Telemetry provider for agent operations.
/// </summary>
public sealed class AgentTelemetry : IDisposable
{
    private readonly TelemetryConfig _config;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;

    // Metrics
    private readonly Counter<long> _requestCounter;
    private readonly Counter<long> _errorCounter;
    private readonly Histogram<double> _latencyHistogram;
    private readonly Histogram<int> _tokenHistogram;
    private readonly UpDownCounter<int> _activeRequestsCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentTelemetry"/> class.
    /// </summary>
    public AgentTelemetry(TelemetryConfig config)
    {
        _config = config;

        // Create ActivitySource for tracing
        _activitySource = new ActivitySource(config.ServiceName, config.ServiceVersion);

        // Create Meter for metrics
        _meter = new Meter(config.ServiceName, config.ServiceVersion);

        // Initialize metrics
        _requestCounter = _meter.CreateCounter<long>(
            "agent.requests.total",
            description: "Total number of agent requests"
        );

        _errorCounter = _meter.CreateCounter<long>(
            "agent.errors.total",
            description: "Total number of agent errors"
        );

        _latencyHistogram = _meter.CreateHistogram<double>(
            "agent.request.duration",
            unit: "ms",
            description: "Agent request duration in milliseconds"
        );

        _tokenHistogram = _meter.CreateHistogram<int>(
            "agent.tokens.used",
            unit: "tokens",
            description: "Number of tokens used per request"
        );

        _activeRequestsCounter = _meter.CreateUpDownCounter<int>(
            "agent.requests.active",
            description: "Number of active agent requests"
        );
    }

    /// <summary>
    /// Starts a new trace span for an agent operation.
    /// </summary>
    public Activity? StartAgentSpan(string agentName, string operation)
    {
        if (!_config.EnableTracing)
        {
            return null;
        }

        var activity = _activitySource.StartActivity(
            $"{agentName}.{operation}",
            ActivityKind.Internal
        );

        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("agent.operation", operation);
        activity?.SetTag("service.environment", _config.Environment);

        return activity;
    }

    /// <summary>
    /// Records a request.
    /// </summary>
    public void RecordRequest(
        string agentName,
        bool success,
        double durationMs,
        int? tokensUsed = null
    )
    {
        if (!_config.EnableMetrics)
        {
            return;
        }

        var tags = new TagList
        {
            { "agent.name", agentName },
            { "success", success ? "true" : "false" },
        };

        _requestCounter.Add(1, tags);
        _latencyHistogram.Record(durationMs, tags);

        if (!success)
        {
            _errorCounter.Add(1, tags);
        }

        if (tokensUsed.HasValue)
        {
            _tokenHistogram.Record(tokensUsed.Value, tags);
        }
    }

    /// <summary>
    /// Tracks an active request.
    /// </summary>
    public IDisposable TrackActiveRequest(string agentName)
    {
        if (!_config.EnableMetrics)
        {
            return new NoOpDisposable();
        }

        var tags = new TagList { { "agent.name", agentName } };
        _activeRequestsCounter.Add(1, tags);

        return new ActiveRequestTracker(() => _activeRequestsCounter.Add(-1, tags));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private class ActiveRequestTracker : IDisposable
    {
        private Action? _onDispose;

        public ActiveRequestTracker(Action onDispose) => _onDispose = onDispose;

        public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }
}
