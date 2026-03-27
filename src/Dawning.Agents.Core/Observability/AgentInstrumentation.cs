using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Dawning.Agents.Core.Observability;

/// <summary>
/// Agent observability metrics and trace sources.
/// </summary>
public static class AgentInstrumentation
{
    public const string ServiceName = "Dawning.Agents";
    public const string ServiceVersion = "1.0.0";

    // Trace source
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    // Metrics source
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    // Counters — gen_ai.* semantic conventions (experimental, v0.29.0+)
    public static readonly Counter<long> RequestsTotal = Meter.CreateCounter<long>(
        "gen_ai.agent.invocations",
        description: "Total number of agent invocations"
    );

    public static readonly Counter<long> RequestsSuccessTotal = Meter.CreateCounter<long>(
        "gen_ai.agent.invocations.success",
        description: "Total number of successful agent invocations"
    );

    public static readonly Counter<long> RequestsFailedTotal = Meter.CreateCounter<long>(
        "gen_ai.agent.invocations.error",
        description: "Total number of failed agent invocations"
    );

    public static readonly Counter<long> ToolExecutionsTotal = Meter.CreateCounter<long>(
        "gen_ai.tool.invocations",
        description: "Total number of tool invocations"
    );

    public static readonly Counter<long> LLMCallsTotal = Meter.CreateCounter<long>(
        "gen_ai.client.operation.duration.count",
        description: "Total number of GenAI client operations"
    );

    public static readonly Counter<long> LLMTokensUsedTotal = Meter.CreateCounter<long>(
        "gen_ai.client.token.usage",
        description: "Total number of GenAI tokens used"
    );

    // Histograms — gen_ai.* semantic conventions (experimental, v0.29.0+)
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "gen_ai.agent.duration",
        unit: "s",
        description: "Duration of agent invocations in seconds"
    );

    public static readonly Histogram<double> ToolExecutionDuration = Meter.CreateHistogram<double>(
        "gen_ai.tool.duration",
        unit: "s",
        description: "Duration of tool executions in seconds"
    );

    public static readonly Histogram<double> LLMCallDuration = Meter.CreateHistogram<double>(
        "gen_ai.client.operation.duration",
        unit: "s",
        description: "Duration of GenAI client operations in seconds"
    );

    // Gauges
    public static readonly ObservableGauge<int> QueueDepth = Meter.CreateObservableGauge(
        "agent_queue_depth",
        () => Volatile.Read(ref _queueDepthCallback)?.Invoke() ?? 0,
        description: "Current depth of the agent request queue"
    );

    public static readonly ObservableGauge<int> ActiveRequests = Meter.CreateObservableGauge(
        "agent_active_requests",
        () => Volatile.Read(ref _activeRequestsCallback)?.Invoke() ?? 0,
        description: "Number of currently active agent requests"
    );

    public static readonly ObservableGauge<int> HealthyInstances = Meter.CreateObservableGauge(
        "agent_healthy_instances",
        () => Volatile.Read(ref _healthyInstancesCallback)?.Invoke() ?? 0,
        description: "Number of healthy agent instances"
    );

    private static Func<int>? _queueDepthCallback;
    private static Func<int>? _activeRequestsCallback;
    private static Func<int>? _healthyInstancesCallback;

    /// <summary>
    /// Sets the queue depth callback.
    /// </summary>
    public static void SetQueueDepthCallback(Func<int> callback) =>
        Volatile.Write(ref _queueDepthCallback, callback);

    /// <summary>
    /// Sets the active requests callback.
    /// </summary>
    public static void SetActiveRequestsCallback(Func<int> callback) =>
        Volatile.Write(ref _activeRequestsCallback, callback);

    /// <summary>
    /// Sets the healthy instances callback.
    /// </summary>
    public static void SetHealthyInstancesCallback(Func<int> callback) =>
        Volatile.Write(ref _healthyInstancesCallback, callback);

    /// <summary>
    /// Starts an agent request trace.
    /// </summary>
    public static Activity? StartAgentRequest(string agentName, string input)
    {
        var activity = ActivitySource.StartActivity("gen_ai.agent.run", ActivityKind.Server);
        activity?.SetTag("gen_ai.agent.name", agentName);
        activity?.SetTag("gen_ai.request.input.length", input.Length);
        return activity;
    }

    /// <summary>
    /// Starts a tool execution trace.
    /// </summary>
    public static Activity? StartToolExecution(string toolName)
    {
        var activity = ActivitySource.StartActivity("gen_ai.tool.execute", ActivityKind.Internal);
        activity?.SetTag("gen_ai.tool.name", toolName);
        return activity;
    }

    /// <summary>
    /// Starts an LLM call trace.
    /// </summary>
    public static Activity? StartLLMCall(
        string provider,
        string model,
        int? maxTokens = null,
        double? temperature = null
    )
    {
        var activity = ActivitySource.StartActivity("gen_ai.chat", ActivityKind.Client);
        activity?.SetTag("gen_ai.system", provider);
        activity?.SetTag("gen_ai.request.model", model);
        if (maxTokens.HasValue)
        {
            activity?.SetTag("gen_ai.request.max_tokens", maxTokens.Value);
        }
        if (temperature.HasValue)
        {
            activity?.SetTag("gen_ai.request.temperature", temperature.Value);
        }
        return activity;
    }

    /// <summary>
    /// Records an exception on the activity.
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
