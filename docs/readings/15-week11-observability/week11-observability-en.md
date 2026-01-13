# Week 11: Observability & Monitoring

> Phase 6: Production Readiness
> Week 11 Learning Materials: Logging, Metrics, Tracing & Dashboards

---

## Days 1-2: Observability Fundamentals

### 1. The Three Pillars of Observability

```
┌─────────────────────────────────────────────────────────────────┐
│                   Observability Pillars                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐    │
│  │     LOGS       │  │    METRICS     │  │    TRACES      │    │
│  │                │  │                │  │                │    │
│  │  What happened │  │  How much/many │  │  Request flow  │    │
│  │  Discrete      │  │  Aggregatable  │  │  Distributed   │    │
│  │  events        │  │  numbers       │  │  context       │    │
│  └────────────────┘  └────────────────┘  └────────────────┘    │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                    DASHBOARDS                            │   │
│  │         Unified view of system health                    │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Telemetry Configuration

```csharp
namespace DawningAgents.Core.Observability;

/// <summary>
/// Telemetry configuration for agents
/// </summary>
public record TelemetryConfig
{
    /// <summary>
    /// Enable logging
    /// </summary>
    public bool EnableLogging { get; init; } = true;
    
    /// <summary>
    /// Enable metrics collection
    /// </summary>
    public bool EnableMetrics { get; init; } = true;
    
    /// <summary>
    /// Enable distributed tracing
    /// </summary>
    public bool EnableTracing { get; init; } = true;
    
    /// <summary>
    /// Service name for telemetry
    /// </summary>
    public string ServiceName { get; init; } = "DawningAgents";
    
    /// <summary>
    /// Service version
    /// </summary>
    public string ServiceVersion { get; init; } = "1.0.0";
    
    /// <summary>
    /// Environment (dev, staging, prod)
    /// </summary>
    public string Environment { get; init; } = "development";
    
    /// <summary>
    /// Log level threshold
    /// </summary>
    public LogLevel MinLogLevel { get; init; } = LogLevel.Information;
    
    /// <summary>
    /// Sample rate for traces (0.0 - 1.0)
    /// </summary>
    public double TraceSampleRate { get; init; } = 1.0;
    
    /// <summary>
    /// OTLP endpoint for exporting telemetry
    /// </summary>
    public string? OtlpEndpoint { get; init; }
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}
```

### 3. Agent Telemetry Provider

```csharp
namespace DawningAgents.Core.Observability;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Provides telemetry instrumentation for agents
/// </summary>
public class AgentTelemetry : IDisposable
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

    public AgentTelemetry(TelemetryConfig config)
    {
        _config = config;
        
        // Create ActivitySource for tracing
        _activitySource = new ActivitySource(
            config.ServiceName,
            config.ServiceVersion);
        
        // Create Meter for metrics
        _meter = new Meter(
            config.ServiceName,
            config.ServiceVersion);
        
        // Initialize metrics
        _requestCounter = _meter.CreateCounter<long>(
            "agent.requests.total",
            description: "Total number of agent requests");
        
        _errorCounter = _meter.CreateCounter<long>(
            "agent.errors.total",
            description: "Total number of agent errors");
        
        _latencyHistogram = _meter.CreateHistogram<double>(
            "agent.request.duration",
            unit: "ms",
            description: "Agent request duration in milliseconds");
        
        _tokenHistogram = _meter.CreateHistogram<int>(
            "agent.tokens.used",
            unit: "tokens",
            description: "Tokens used per request");
        
        _activeRequestsCounter = _meter.CreateUpDownCounter<int>(
            "agent.requests.active",
            description: "Number of active agent requests");
    }

    /// <summary>
    /// Start a new trace span for agent execution
    /// </summary>
    public Activity? StartAgentSpan(string agentName, string operation)
    {
        if (!_config.EnableTracing)
            return null;
            
        var activity = _activitySource.StartActivity(
            $"{agentName}.{operation}",
            ActivityKind.Internal);
        
        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("agent.operation", operation);
        activity?.SetTag("service.environment", _config.Environment);
        
        return activity;
    }

    /// <summary>
    /// Record a request
    /// </summary>
    public void RecordRequest(string agentName, bool success, double durationMs, int? tokensUsed = null)
    {
        if (!_config.EnableMetrics)
            return;
            
        var tags = new TagList
        {
            { "agent.name", agentName },
            { "success", success.ToString().ToLower() }
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
    /// Track active requests
    /// </summary>
    public IDisposable TrackActiveRequest(string agentName)
    {
        if (!_config.EnableMetrics)
            return new NoOpDisposable();
            
        var tags = new TagList { { "agent.name", agentName } };
        _activeRequestsCounter.Add(1, tags);
        
        return new ActiveRequestTracker(() => _activeRequestsCounter.Add(-1, tags));
    }

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
        private readonly Action _onDispose;
        public ActiveRequestTracker(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
```

---

## Days 3-4: Structured Logging

### 1. Agent Logger

```csharp
namespace DawningAgents.Core.Observability;

using Microsoft.Extensions.Logging;

/// <summary>
/// Structured logger for agents
/// </summary>
public class AgentLogger
{
    private readonly ILogger _logger;
    private readonly string _agentName;
    private readonly TelemetryConfig _config;

    public AgentLogger(ILogger logger, string agentName, TelemetryConfig config)
    {
        _logger = logger;
        _agentName = agentName;
        _config = config;
    }

    public void LogRequestStart(string requestId, string input)
    {
        if (!_config.EnableLogging) return;
        
        _logger.LogInformation(
            "Agent {AgentName} starting request {RequestId}. Input length: {InputLength}",
            _agentName, requestId, input.Length);
    }

    public void LogRequestComplete(string requestId, bool success, TimeSpan duration, int? tokensUsed = null)
    {
        if (!_config.EnableLogging) return;
        
        if (success)
        {
            _logger.LogInformation(
                "Agent {AgentName} completed request {RequestId} in {DurationMs}ms. Tokens: {TokensUsed}",
                _agentName, requestId, duration.TotalMilliseconds, tokensUsed ?? 0);
        }
        else
        {
            _logger.LogWarning(
                "Agent {AgentName} failed request {RequestId} after {DurationMs}ms",
                _agentName, requestId, duration.TotalMilliseconds);
        }
    }

    public void LogToolCall(string requestId, string toolName, bool success, TimeSpan duration)
    {
        if (!_config.EnableLogging) return;
        
        _logger.LogDebug(
            "Agent {AgentName} called tool {ToolName} for request {RequestId}. Success: {Success}, Duration: {DurationMs}ms",
            _agentName, toolName, requestId, success, duration.TotalMilliseconds);
    }

    public void LogIteration(string requestId, int iteration, string thought)
    {
        if (!_config.EnableLogging) return;
        
        _logger.LogDebug(
            "Agent {AgentName} iteration {Iteration} for request {RequestId}. Thought: {Thought}",
            _agentName, iteration, requestId, thought.Length > 100 ? thought[..100] + "..." : thought);
    }

    public void LogError(string requestId, Exception ex, string context)
    {
        if (!_config.EnableLogging) return;
        
        _logger.LogError(ex,
            "Agent {AgentName} error in request {RequestId}. Context: {Context}",
            _agentName, requestId, context);
    }

    public void LogGuardrailTriggered(string requestId, string guardrailName, string action, string? reason = null)
    {
        if (!_config.EnableLogging) return;
        
        _logger.LogWarning(
            "Agent {AgentName} guardrail {GuardrailName} triggered for request {RequestId}. Action: {Action}, Reason: {Reason}",
            _agentName, guardrailName, requestId, action, reason ?? "N/A");
    }

    public void LogLLMCall(string requestId, string model, int promptTokens, int completionTokens, TimeSpan duration)
    {
        if (!_config.EnableLogging) return;
        
        _logger.LogDebug(
            "Agent {AgentName} LLM call for request {RequestId}. Model: {Model}, Prompt: {PromptTokens}, Completion: {CompletionTokens}, Duration: {DurationMs}ms",
            _agentName, requestId, model, promptTokens, completionTokens, duration.TotalMilliseconds);
    }
}
```

### 2. Log Context & Enrichment

```csharp
namespace DawningAgents.Core.Observability;

using System.Collections.Concurrent;

/// <summary>
/// Provides contextual information for logging
/// </summary>
public class LogContext : IDisposable
{
    private static readonly AsyncLocal<LogContext?> _current = new();
    private readonly ConcurrentDictionary<string, object> _properties = new();
    private readonly LogContext? _parent;

    public static LogContext? Current => _current.Value;

    private LogContext(LogContext? parent = null)
    {
        _parent = parent;
    }

    /// <summary>
    /// Push a new log context scope
    /// </summary>
    public static LogContext Push()
    {
        var context = new LogContext(_current.Value);
        _current.Value = context;
        return context;
    }

    /// <summary>
    /// Set a context property
    /// </summary>
    public LogContext Set(string key, object value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Get all properties including parent contexts
    /// </summary>
    public IDictionary<string, object> GetAllProperties()
    {
        var result = new Dictionary<string, object>();
        
        // Get parent properties first
        if (_parent != null)
        {
            foreach (var (key, value) in _parent.GetAllProperties())
            {
                result[key] = value;
            }
        }
        
        // Override with current context
        foreach (var (key, value) in _properties)
        {
            result[key] = value;
        }
        
        return result;
    }

    public void Dispose()
    {
        _current.Value = _parent;
    }
}

/// <summary>
/// Extensions for log context
/// </summary>
public static class LogContextExtensions
{
    public static LogContext WithRequestId(this LogContext context, string requestId)
        => context.Set("RequestId", requestId);
    
    public static LogContext WithAgentName(this LogContext context, string agentName)
        => context.Set("AgentName", agentName);
    
    public static LogContext WithUserId(this LogContext context, string userId)
        => context.Set("UserId", userId);
    
    public static LogContext WithSessionId(this LogContext context, string sessionId)
        => context.Set("SessionId", sessionId);
    
    public static LogContext WithCorrelationId(this LogContext context, string correlationId)
        => context.Set("CorrelationId", correlationId);
}
```

---

## Days 5-7: Metrics & Tracing

### 1. Metrics Collector

```csharp
namespace DawningAgents.Core.Observability;

using System.Collections.Concurrent;

/// <summary>
/// In-memory metrics collector for development/testing
/// </summary>
public class MetricsCollector
{
    private readonly ConcurrentDictionary<string, CounterMetric> _counters = new();
    private readonly ConcurrentDictionary<string, HistogramMetric> _histograms = new();
    private readonly ConcurrentDictionary<string, GaugeMetric> _gauges = new();

    /// <summary>
    /// Increment a counter
    /// </summary>
    public void IncrementCounter(string name, long value = 1, IDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        var counter = _counters.GetOrAdd(key, _ => new CounterMetric(name, tags));
        counter.Add(value);
    }

    /// <summary>
    /// Record a histogram value
    /// </summary>
    public void RecordHistogram(string name, double value, IDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        var histogram = _histograms.GetOrAdd(key, _ => new HistogramMetric(name, tags));
        histogram.Record(value);
    }

    /// <summary>
    /// Set a gauge value
    /// </summary>
    public void SetGauge(string name, double value, IDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        var gauge = _gauges.GetOrAdd(key, _ => new GaugeMetric(name, tags));
        gauge.Set(value);
    }

    /// <summary>
    /// Get all metrics snapshot
    /// </summary>
    public MetricsSnapshot GetSnapshot()
    {
        return new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Counters = _counters.Values.Select(c => c.ToData()).ToList(),
            Histograms = _histograms.Values.Select(h => h.ToData()).ToList(),
            Gauges = _gauges.Values.Select(g => g.ToData()).ToList()
        };
    }

    private static string GetMetricKey(string name, IDictionary<string, string>? tags)
    {
        if (tags == null || tags.Count == 0)
            return name;
            
        var tagString = string.Join(",", tags.OrderBy(t => t.Key).Select(t => $"{t.Key}={t.Value}"));
        return $"{name}|{tagString}";
    }
}

public class CounterMetric
{
    private long _value;
    public string Name { get; }
    public IDictionary<string, string>? Tags { get; }
    
    public CounterMetric(string name, IDictionary<string, string>? tags)
    {
        Name = name;
        Tags = tags;
    }
    
    public void Add(long value) => Interlocked.Add(ref _value, value);
    
    public MetricData ToData() => new()
    {
        Name = Name,
        Type = "counter",
        Value = _value,
        Tags = Tags
    };
}

public class HistogramMetric
{
    private readonly List<double> _values = [];
    private readonly object _lock = new();
    public string Name { get; }
    public IDictionary<string, string>? Tags { get; }
    
    public HistogramMetric(string name, IDictionary<string, string>? tags)
    {
        Name = name;
        Tags = tags;
    }
    
    public void Record(double value)
    {
        lock (_lock) { _values.Add(value); }
    }
    
    public MetricData ToData()
    {
        lock (_lock)
        {
            var sorted = _values.OrderBy(v => v).ToList();
            return new MetricData
            {
                Name = Name,
                Type = "histogram",
                Count = sorted.Count,
                Sum = sorted.Sum(),
                Min = sorted.Count > 0 ? sorted[0] : 0,
                Max = sorted.Count > 0 ? sorted[^1] : 0,
                P50 = GetPercentile(sorted, 0.5),
                P95 = GetPercentile(sorted, 0.95),
                P99 = GetPercentile(sorted, 0.99),
                Tags = Tags
            };
        }
    }
    
    private static double GetPercentile(List<double> sorted, double percentile)
    {
        if (sorted.Count == 0) return 0;
        var index = (int)(percentile * (sorted.Count - 1));
        return sorted[index];
    }
}

public class GaugeMetric
{
    private double _value;
    public string Name { get; }
    public IDictionary<string, string>? Tags { get; }
    
    public GaugeMetric(string name, IDictionary<string, string>? tags)
    {
        Name = name;
        Tags = tags;
    }
    
    public void Set(double value) => Interlocked.Exchange(ref _value, value);
    
    public MetricData ToData() => new()
    {
        Name = Name,
        Type = "gauge",
        Value = _value,
        Tags = Tags
    };
}

public record MetricsSnapshot
{
    public DateTime Timestamp { get; init; }
    public IReadOnlyList<MetricData> Counters { get; init; } = [];
    public IReadOnlyList<MetricData> Histograms { get; init; } = [];
    public IReadOnlyList<MetricData> Gauges { get; init; } = [];
}

public record MetricData
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public double Value { get; init; }
    public int Count { get; init; }
    public double Sum { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double P50 { get; init; }
    public double P95 { get; init; }
    public double P99 { get; init; }
    public IDictionary<string, string>? Tags { get; init; }
}
```

### 2. Distributed Tracing

```csharp
namespace DawningAgents.Core.Observability;

using System.Diagnostics;

/// <summary>
/// Distributed tracing utilities
/// </summary>
public class DistributedTracer
{
    private readonly ActivitySource _source;
    private readonly TelemetryConfig _config;

    public DistributedTracer(TelemetryConfig config)
    {
        _config = config;
        _source = new ActivitySource(config.ServiceName, config.ServiceVersion);
    }

    /// <summary>
    /// Start a new trace span
    /// </summary>
    public ITraceSpan StartSpan(string name, SpanKind kind = SpanKind.Internal)
    {
        if (!_config.EnableTracing)
            return new NoOpSpan();
            
        var activityKind = kind switch
        {
            SpanKind.Client => ActivityKind.Client,
            SpanKind.Server => ActivityKind.Server,
            SpanKind.Producer => ActivityKind.Producer,
            SpanKind.Consumer => ActivityKind.Consumer,
            _ => ActivityKind.Internal
        };

        var activity = _source.StartActivity(name, activityKind);
        if (activity == null)
            return new NoOpSpan();
            
        return new ActivitySpan(activity);
    }

    /// <summary>
    /// Get current trace context for propagation
    /// </summary>
    public TraceContext? GetCurrentContext()
    {
        var activity = Activity.Current;
        if (activity == null)
            return null;
            
        return new TraceContext
        {
            TraceId = activity.TraceId.ToString(),
            SpanId = activity.SpanId.ToString(),
            TraceFlags = activity.ActivityTraceFlags.ToString()
        };
    }

    /// <summary>
    /// Continue from propagated context
    /// </summary>
    public ITraceSpan StartSpanFromContext(string name, TraceContext context, SpanKind kind = SpanKind.Internal)
    {
        if (!_config.EnableTracing)
            return new NoOpSpan();
            
        var traceId = ActivityTraceId.CreateFromString(context.TraceId.AsSpan());
        var spanId = ActivitySpanId.CreateFromString(context.SpanId.AsSpan());
        var traceFlags = Enum.TryParse<ActivityTraceFlags>(context.TraceFlags, out var flags) 
            ? flags 
            : ActivityTraceFlags.None;

        var parentContext = new ActivityContext(traceId, spanId, traceFlags);
        
        var activity = _source.StartActivity(
            name,
            kind switch
            {
                SpanKind.Client => ActivityKind.Client,
                SpanKind.Server => ActivityKind.Server,
                _ => ActivityKind.Internal
            },
            parentContext);
            
        if (activity == null)
            return new NoOpSpan();
            
        return new ActivitySpan(activity);
    }
}

public enum SpanKind
{
    Internal,
    Client,
    Server,
    Producer,
    Consumer
}

public record TraceContext
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public required string TraceFlags { get; init; }
}

public interface ITraceSpan : IDisposable
{
    string SpanId { get; }
    void SetAttribute(string key, object value);
    void SetStatus(SpanStatus status, string? description = null);
    void RecordException(Exception ex);
    ITraceSpan StartChildSpan(string name);
}

public enum SpanStatus
{
    Ok,
    Error,
    Unset
}

internal class ActivitySpan : ITraceSpan
{
    private readonly Activity _activity;

    public ActivitySpan(Activity activity)
    {
        _activity = activity;
    }

    public string SpanId => _activity.SpanId.ToString();

    public void SetAttribute(string key, object value)
    {
        _activity.SetTag(key, value);
    }

    public void SetStatus(SpanStatus status, string? description = null)
    {
        _activity.SetStatus(status switch
        {
            SpanStatus.Ok => ActivityStatusCode.Ok,
            SpanStatus.Error => ActivityStatusCode.Error,
            _ => ActivityStatusCode.Unset
        }, description);
    }

    public void RecordException(Exception ex)
    {
        _activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        _activity.SetTag("exception.type", ex.GetType().FullName);
        _activity.SetTag("exception.message", ex.Message);
        _activity.SetTag("exception.stacktrace", ex.StackTrace);
    }

    public ITraceSpan StartChildSpan(string name)
    {
        var childActivity = _activity.Source.StartActivity(name, ActivityKind.Internal, _activity.Context);
        if (childActivity == null)
            return new NoOpSpan();
        return new ActivitySpan(childActivity);
    }

    public void Dispose()
    {
        _activity.Dispose();
    }
}

internal class NoOpSpan : ITraceSpan
{
    public string SpanId => "";
    public void SetAttribute(string key, object value) { }
    public void SetStatus(SpanStatus status, string? description = null) { }
    public void RecordException(Exception ex) { }
    public ITraceSpan StartChildSpan(string name) => this;
    public void Dispose() { }
}
```

### 3. Observable Agent Wrapper

```csharp
namespace DawningAgents.Core.Observability;

using Microsoft.Extensions.Logging;

/// <summary>
/// Wraps an agent with full observability
/// </summary>
public class ObservableAgent : IAgent
{
    private readonly IAgent _innerAgent;
    private readonly AgentTelemetry _telemetry;
    private readonly AgentLogger _agentLogger;
    private readonly DistributedTracer _tracer;
    private readonly MetricsCollector _metrics;

    public string Name => $"Observable({_innerAgent.Name})";

    public ObservableAgent(
        IAgent innerAgent,
        AgentTelemetry telemetry,
        ILogger logger,
        TelemetryConfig config)
    {
        _innerAgent = innerAgent;
        _telemetry = telemetry;
        _agentLogger = new AgentLogger(logger, innerAgent.Name, config);
        _tracer = new DistributedTracer(config);
        _metrics = new MetricsCollector();
    }

    public async Task<AgentResponse> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTime.UtcNow;

        // Start trace span
        using var span = _tracer.StartSpan($"{_innerAgent.Name}.Execute", SpanKind.Internal);
        span.SetAttribute("request.id", requestId);
        span.SetAttribute("input.length", context.Input.Length);

        // Set up log context
        using var logContext = LogContext.Push()
            .WithRequestId(requestId)
            .WithAgentName(_innerAgent.Name);

        // Track active requests
        using var activeTracker = _telemetry.TrackActiveRequest(_innerAgent.Name);

        _agentLogger.LogRequestStart(requestId, context.Input);

        try
        {
            var response = await _innerAgent.ExecuteAsync(context, cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            // Record success metrics
            _telemetry.RecordRequest(
                _innerAgent.Name, 
                response.IsSuccess, 
                duration.TotalMilliseconds,
                response.Metadata.TryGetValue("tokens_used", out var tokens) ? (int?)tokens : null);

            _metrics.IncrementCounter("agent.requests", 1, new Dictionary<string, string>
            {
                ["agent"] = _innerAgent.Name,
                ["success"] = response.IsSuccess.ToString().ToLower()
            });

            _metrics.RecordHistogram("agent.latency_ms", duration.TotalMilliseconds, 
                new Dictionary<string, string> { ["agent"] = _innerAgent.Name });

            _agentLogger.LogRequestComplete(requestId, response.IsSuccess, duration);

            span.SetAttribute("response.success", response.IsSuccess);
            span.SetAttribute("response.length", response.Output.Length);
            span.SetStatus(response.IsSuccess ? SpanStatus.Ok : SpanStatus.Error);

            return response with
            {
                Metadata = new Dictionary<string, object>(response.Metadata)
                {
                    ["request_id"] = requestId,
                    ["trace_id"] = span.SpanId
                }
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;

            _telemetry.RecordRequest(_innerAgent.Name, false, duration.TotalMilliseconds);
            _metrics.IncrementCounter("agent.errors", 1, new Dictionary<string, string>
            {
                ["agent"] = _innerAgent.Name,
                ["error_type"] = ex.GetType().Name
            });

            _agentLogger.LogError(requestId, ex, "Agent execution failed");

            span.RecordException(ex);
            span.SetStatus(SpanStatus.Error, ex.Message);

            throw;
        }
    }

    /// <summary>
    /// Get current metrics snapshot
    /// </summary>
    public MetricsSnapshot GetMetrics() => _metrics.GetSnapshot();
}
```

### 4. Health Check

```csharp
namespace DawningAgents.Core.Observability;

/// <summary>
/// Health check for agent system
/// </summary>
public class AgentHealthCheck
{
    private readonly List<IHealthCheckProvider> _providers = [];
    
    public void AddProvider(IHealthCheckProvider provider)
    {
        _providers.Add(provider);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ComponentHealth>();
        var overallHealthy = true;

        foreach (var provider in _providers)
        {
            try
            {
                var health = await provider.CheckHealthAsync(cancellationToken);
                results.Add(health);
                if (health.Status != HealthStatus.Healthy)
                    overallHealthy = false;
            }
            catch (Exception ex)
            {
                results.Add(new ComponentHealth
                {
                    Name = provider.Name,
                    Status = HealthStatus.Unhealthy,
                    Message = ex.Message
                });
                overallHealthy = false;
            }
        }

        return new HealthCheckResult
        {
            Status = overallHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            Timestamp = DateTime.UtcNow,
            Components = results
        };
    }
}

public interface IHealthCheckProvider
{
    string Name { get; }
    Task<ComponentHealth> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public record HealthCheckResult
{
    public HealthStatus Status { get; init; }
    public DateTime Timestamp { get; init; }
    public IReadOnlyList<ComponentHealth> Components { get; init; } = [];
}

public record ComponentHealth
{
    public required string Name { get; init; }
    public HealthStatus Status { get; init; }
    public string? Message { get; init; }
    public IDictionary<string, object>? Data { get; init; }
}

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// LLM provider health check
/// </summary>
public class LLMHealthCheck : IHealthCheckProvider
{
    private readonly ILLMProvider _llm;
    public string Name => "LLM";

    public LLMHealthCheck(ILLMProvider llm)
    {
        _llm = llm;
    }

    public async Task<ComponentHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var start = DateTime.UtcNow;
            await _llm.GenerateAsync("Hello", maxTokens: 5, cancellationToken: cancellationToken);
            var latency = DateTime.UtcNow - start;

            return new ComponentHealth
            {
                Name = Name,
                Status = latency.TotalSeconds < 5 ? HealthStatus.Healthy : HealthStatus.Degraded,
                Message = $"Response time: {latency.TotalMilliseconds:F0}ms",
                Data = new Dictionary<string, object>
                {
                    ["latency_ms"] = latency.TotalMilliseconds
                }
            };
        }
        catch (Exception ex)
        {
            return new ComponentHealth
            {
                Name = Name,
                Status = HealthStatus.Unhealthy,
                Message = ex.Message
            };
        }
    }
}
```

---

## Complete Example

```csharp
// Configure telemetry
var telemetryConfig = new TelemetryConfig
{
    ServiceName = "DawningAgents",
    ServiceVersion = "1.0.0",
    Environment = "development",
    EnableLogging = true,
    EnableMetrics = true,
    EnableTracing = true,
    TraceSampleRate = 1.0
};

// Create telemetry providers
var telemetry = new AgentTelemetry(telemetryConfig);
var logger = loggerFactory.CreateLogger<ObservableAgent>();

// Create observable agent
var innerAgent = new ReActAgent(llm, loggerFactory.CreateLogger<ReActAgent>());
var observableAgent = new ObservableAgent(innerAgent, telemetry, logger, telemetryConfig);

// Set up health checks
var healthCheck = new AgentHealthCheck();
healthCheck.AddProvider(new LLMHealthCheck(llm));

// Execute with full observability
using (LogContext.Push()
    .WithUserId("user-123")
    .WithSessionId("session-456"))
{
    var response = await observableAgent.ExecuteAsync(new AgentContext
    {
        Input = "Analyze the current market trends"
    });

    Console.WriteLine($"Response: {response.Output}");
    Console.WriteLine($"Request ID: {response.Metadata["request_id"]}");
}

// Get metrics
var metrics = observableAgent.GetMetrics();
Console.WriteLine($"\n=== Metrics Snapshot ===");
foreach (var counter in metrics.Counters)
{
    Console.WriteLine($"{counter.Name}: {counter.Value}");
}
foreach (var histogram in metrics.Histograms)
{
    Console.WriteLine($"{histogram.Name}: count={histogram.Count}, p50={histogram.P50:F2}ms, p99={histogram.P99:F2}ms");
}

// Check health
var health = await healthCheck.CheckHealthAsync();
Console.WriteLine($"\n=== Health Check ===");
Console.WriteLine($"Status: {health.Status}");
foreach (var component in health.Components)
{
    Console.WriteLine($"  {component.Name}: {component.Status} - {component.Message}");
}
```

---

## Summary

### Week 11 Deliverables

```
src/DawningAgents.Core/
└── Observability/
    ├── TelemetryConfig.cs        # Configuration
    ├── AgentTelemetry.cs         # Metrics/tracing provider
    ├── AgentLogger.cs            # Structured logging
    ├── LogContext.cs             # Log context/enrichment
    ├── MetricsCollector.cs       # In-memory metrics
    ├── DistributedTracer.cs      # Distributed tracing
    ├── ObservableAgent.cs        # Observable wrapper
    └── HealthCheck.cs            # Health checks
```

### Observability Components

| Component | Purpose |
|-----------|---------|
| **Logging** | Structured event recording |
| **Metrics** | Quantitative measurements |
| **Tracing** | Request flow tracking |
| **Health Checks** | System health monitoring |

### Key Metrics to Track

| Metric | Type | Description |
|--------|------|-------------|
| `agent.requests.total` | Counter | Total requests |
| `agent.errors.total` | Counter | Total errors |
| `agent.request.duration` | Histogram | Latency (ms) |
| `agent.tokens.used` | Histogram | Token consumption |
| `agent.requests.active` | Gauge | Active requests |

### Next: Week 12

Week 12 will cover Deployment & Scaling:
- Containerization
- Configuration management
- Scaling strategies
- Production deployment
