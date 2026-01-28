using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Dawning.Agents.Core.Observability;

/// <summary>
/// Agent 可观测性指标和追踪源
/// </summary>
public static class AgentInstrumentation
{
    public const string ServiceName = "Dawning.Agents";
    public const string ServiceVersion = "1.0.0";

    // 追踪源
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    // 指标源
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    // 计数器
    public static readonly Counter<long> RequestsTotal = Meter.CreateCounter<long>(
        "agent_requests_total",
        description: "Total number of agent requests"
    );

    public static readonly Counter<long> RequestsSuccessTotal = Meter.CreateCounter<long>(
        "agent_requests_success_total",
        description: "Total number of successful agent requests"
    );

    public static readonly Counter<long> RequestsFailedTotal = Meter.CreateCounter<long>(
        "agent_requests_failed_total",
        description: "Total number of failed agent requests"
    );

    public static readonly Counter<long> ToolExecutionsTotal = Meter.CreateCounter<long>(
        "agent_tool_executions_total",
        description: "Total number of tool executions"
    );

    public static readonly Counter<long> LLMCallsTotal = Meter.CreateCounter<long>(
        "llm_calls_total",
        description: "Total number of LLM API calls"
    );

    public static readonly Counter<long> LLMTokensUsedTotal = Meter.CreateCounter<long>(
        "llm_tokens_used_total",
        description: "Total number of LLM tokens used"
    );

    // 直方图
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "agent_request_duration_seconds",
        unit: "s",
        description: "Duration of agent requests in seconds"
    );

    public static readonly Histogram<double> ToolExecutionDuration = Meter.CreateHistogram<double>(
        "agent_tool_execution_duration_seconds",
        unit: "s",
        description: "Duration of tool executions in seconds"
    );

    public static readonly Histogram<double> LLMCallDuration = Meter.CreateHistogram<double>(
        "llm_call_duration_seconds",
        unit: "s",
        description: "Duration of LLM API calls in seconds"
    );

    // 仪表盘
    public static readonly ObservableGauge<int> QueueDepth = Meter.CreateObservableGauge(
        "agent_queue_depth",
        () => _queueDepthCallback?.Invoke() ?? 0,
        description: "Current depth of the agent request queue"
    );

    public static readonly ObservableGauge<int> ActiveRequests = Meter.CreateObservableGauge(
        "agent_active_requests",
        () => _activeRequestsCallback?.Invoke() ?? 0,
        description: "Number of currently active agent requests"
    );

    public static readonly ObservableGauge<int> HealthyInstances = Meter.CreateObservableGauge(
        "agent_healthy_instances",
        () => _healthyInstancesCallback?.Invoke() ?? 0,
        description: "Number of healthy agent instances"
    );

    private static Func<int>? _queueDepthCallback;
    private static Func<int>? _activeRequestsCallback;
    private static Func<int>? _healthyInstancesCallback;

    /// <summary>
    /// 设置队列深度回调
    /// </summary>
    public static void SetQueueDepthCallback(Func<int> callback) => _queueDepthCallback = callback;

    /// <summary>
    /// 设置活跃请求数回调
    /// </summary>
    public static void SetActiveRequestsCallback(Func<int> callback) =>
        _activeRequestsCallback = callback;

    /// <summary>
    /// 设置健康实例数回调
    /// </summary>
    public static void SetHealthyInstancesCallback(Func<int> callback) =>
        _healthyInstancesCallback = callback;

    /// <summary>
    /// 开始 Agent 请求追踪
    /// </summary>
    public static Activity? StartAgentRequest(string agentName, string input)
    {
        var activity = ActivitySource.StartActivity("agent.request", ActivityKind.Server);
        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("agent.input.length", input.Length);
        return activity;
    }

    /// <summary>
    /// 开始工具执行追踪
    /// </summary>
    public static Activity? StartToolExecution(string toolName)
    {
        var activity = ActivitySource.StartActivity("agent.tool.execute", ActivityKind.Internal);
        activity?.SetTag("tool.name", toolName);
        return activity;
    }

    /// <summary>
    /// 开始 LLM 调用追踪
    /// </summary>
    public static Activity? StartLLMCall(string provider, string model)
    {
        var activity = ActivitySource.StartActivity("llm.call", ActivityKind.Client);
        activity?.SetTag("llm.provider", provider);
        activity?.SetTag("llm.model", model);
        return activity;
    }

    /// <summary>
    /// 记录异常
    /// </summary>
    public static void RecordException(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddEvent(
            new ActivityEvent(
                "exception",
                tags: new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message },
                    { "exception.stacktrace", ex.StackTrace ?? "" },
                }
            )
        );
    }
}
