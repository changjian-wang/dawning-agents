namespace Dawning.Agents.Core.Observability;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dawning.Agents.Abstractions.Observability;

/// <summary>
/// Agent 遥测提供者
/// </summary>
public sealed class AgentTelemetry : IDisposable
{
    private readonly TelemetryConfig _config;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;

    // 指标
    private readonly Counter<long> _requestCounter;
    private readonly Counter<long> _errorCounter;
    private readonly Histogram<double> _latencyHistogram;
    private readonly Histogram<int> _tokenHistogram;
    private readonly UpDownCounter<int> _activeRequestsCounter;

    /// <summary>
    /// 创建遥测提供者
    /// </summary>
    public AgentTelemetry(TelemetryConfig config)
    {
        _config = config;

        // 创建用于追踪的 ActivitySource
        _activitySource = new ActivitySource(config.ServiceName, config.ServiceVersion);

        // 创建用于指标的 Meter
        _meter = new Meter(config.ServiceName, config.ServiceVersion);

        // 初始化指标
        _requestCounter = _meter.CreateCounter<long>(
            "agent.requests.total",
            description: "Agent 请求总数"
        );

        _errorCounter = _meter.CreateCounter<long>(
            "agent.errors.total",
            description: "Agent 错误总数"
        );

        _latencyHistogram = _meter.CreateHistogram<double>(
            "agent.request.duration",
            unit: "ms",
            description: "Agent 请求时长（毫秒）"
        );

        _tokenHistogram = _meter.CreateHistogram<int>(
            "agent.tokens.used",
            unit: "tokens",
            description: "每个请求使用的 token 数"
        );

        _activeRequestsCounter = _meter.CreateUpDownCounter<int>(
            "agent.requests.active",
            description: "活跃 Agent 请求数"
        );
    }

    /// <summary>
    /// 为 Agent 执行启动新的追踪 span
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
    /// 记录请求
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
            { "success", success.ToString().ToLower() },
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
    /// 跟踪活跃请求
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
        GC.SuppressFinalize(this);
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private class ActiveRequestTracker : IDisposable
    {
        private readonly Action _onDispose;

        public ActiveRequestTracker(Action onDispose) => _onDispose = onDispose;

        public void Dispose() => _onDispose();
    }
}
