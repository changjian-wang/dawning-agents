namespace Dawning.Agents.Core.Observability;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// 用完整可观测性包装 Agent
/// </summary>
public sealed class ObservableAgent : IAgent
{
    private readonly IAgent _innerAgent;
    private readonly AgentTelemetry _telemetry;
    private readonly AgentLogger _agentLogger;
    private readonly DistributedTracer _tracer;
    private readonly MetricsCollector _metrics;

    /// <inheritdoc />
    public string Name => $"Observable({_innerAgent.Name})";

    /// <inheritdoc />
    public string Instructions => _innerAgent.Instructions;

    /// <summary>
    /// 内部 Agent
    /// </summary>
    public IAgent InnerAgent => _innerAgent;

    /// <summary>
    /// 创建可观测 Agent
    /// </summary>
    public ObservableAgent(
        IAgent innerAgent,
        AgentTelemetry telemetry,
        TelemetryConfig config,
        ILogger? logger = null
    )
    {
        _innerAgent = innerAgent;
        _telemetry = telemetry;
        _agentLogger = new AgentLogger(logger ?? NullLogger.Instance, innerAgent.Name, config);
        _tracer = new DistributedTracer(config);
        _metrics = new MetricsCollector();
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var context = new AgentContext { UserInput = input };
        return await RunAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTime.UtcNow;

        // 启动追踪 span
        using var span = _tracer.StartSpan($"{_innerAgent.Name}.Run", SpanKind.Internal);
        span.SetAttribute("request.id", requestId);
        span.SetAttribute("input.length", context.UserInput.Length);

        // 设置日志上下文
        using var logContext = LogContext
            .Push()
            .WithRequestId(requestId)
            .WithAgentName(_innerAgent.Name)
            .WithSessionId(context.SessionId);

        // 跟踪活跃请求
        using var activeTracker = _telemetry.TrackActiveRequest(_innerAgent.Name);

        _agentLogger.LogRequestStart(requestId, context.UserInput);

        try
        {
            var response = await _innerAgent.RunAsync(context, cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            // 记录成功指标
            _telemetry.RecordRequest(
                _innerAgent.Name,
                response.Success,
                duration.TotalMilliseconds
            );

            _metrics.IncrementCounter(
                "agent.requests",
                1,
                new Dictionary<string, string>
                {
                    ["agent"] = _innerAgent.Name,
                    ["success"] = response.Success.ToString().ToLower(),
                }
            );

            _metrics.RecordHistogram(
                "agent.latency_ms",
                duration.TotalMilliseconds,
                new Dictionary<string, string> { ["agent"] = _innerAgent.Name }
            );

            _agentLogger.LogRequestComplete(requestId, response.Success, duration);

            span.SetAttribute("response.success", response.Success);
            span.SetAttribute("response.length", response.FinalAnswer?.Length ?? 0);
            span.SetStatus(response.Success ? SpanStatus.Ok : SpanStatus.Error);

            return response;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;

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

            _agentLogger.LogError(requestId, ex, "Agent 执行失败");

            span.RecordException(ex);
            span.SetStatus(SpanStatus.Error, ex.Message);

            throw;
        }
    }

    /// <summary>
    /// 获取当前指标快照
    /// </summary>
    public MetricsSnapshot GetMetrics() => _metrics.GetSnapshot();

    /// <summary>
    /// 获取指标收集器
    /// </summary>
    public MetricsCollector MetricsCollector => _metrics;
}
