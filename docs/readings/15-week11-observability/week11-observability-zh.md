# 第11周：可观测性与监控

> 第六阶段：生产就绪
> 第11周学习材料：日志、指标、追踪与仪表盘

---

## 第1-2天：可观测性基础

### 1. 可观测性三大支柱

```text
┌─────────────────────────────────────────────────────────────────┐
│                      可观测性支柱                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐    │
│  │     日志       │  │     指标       │  │     追踪       │    │
│  │                │  │                │  │                │    │
│  │  发生了什么    │  │  多少/多久     │  │  请求流向      │    │
│  │  离散事件      │  │  可聚合数值    │  │  分布式上下文  │    │
│  └────────────────┘  └────────────────┘  └────────────────┘    │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                      仪表盘                              │   │
│  │              系统健康状态统一视图                        │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. 遥测配置

```csharp
namespace DawningAgents.Core.Observability;

/// <summary>
/// Agent遥测配置
/// </summary>
public record TelemetryConfig
{
    /// <summary>
    /// 启用日志
    /// </summary>
    public bool EnableLogging { get; init; } = true;
    
    /// <summary>
    /// 启用指标收集
    /// </summary>
    public bool EnableMetrics { get; init; } = true;
    
    /// <summary>
    /// 启用分布式追踪
    /// </summary>
    public bool EnableTracing { get; init; } = true;
    
    /// <summary>
    /// 遥测服务名称
    /// </summary>
    public string ServiceName { get; init; } = "DawningAgents";
    
    /// <summary>
    /// 服务版本
    /// </summary>
    public string ServiceVersion { get; init; } = "1.0.0";
    
    /// <summary>
    /// 环境（dev, staging, prod）
    /// </summary>
    public string Environment { get; init; } = "development";
    
    /// <summary>
    /// 日志级别阈值
    /// </summary>
    public LogLevel MinLogLevel { get; init; } = LogLevel.Information;
    
    /// <summary>
    /// 追踪采样率（0.0 - 1.0）
    /// </summary>
    public double TraceSampleRate { get; init; } = 1.0;
    
    /// <summary>
    /// 遥测导出的OTLP端点
    /// </summary>
    public string? OtlpEndpoint { get; init; }
}

public enum LogLevel
{
    Trace,       // 跟踪
    Debug,       // 调试
    Information, // 信息
    Warning,     // 警告
    Error,       // 错误
    Critical     // 严重
}
```

### 3. Agent遥测提供者

```csharp
namespace DawningAgents.Core.Observability;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// 为Agent提供遥测仪表化
/// </summary>
public class AgentTelemetry : IDisposable
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

    public AgentTelemetry(TelemetryConfig config)
    {
        _config = config;
        
        // 创建用于追踪的ActivitySource
        _activitySource = new ActivitySource(
            config.ServiceName,
            config.ServiceVersion);
        
        // 创建用于指标的Meter
        _meter = new Meter(
            config.ServiceName,
            config.ServiceVersion);
        
        // 初始化指标
        _requestCounter = _meter.CreateCounter<long>(
            "agent.requests.total",
            description: "Agent请求总数");
        
        _errorCounter = _meter.CreateCounter<long>(
            "agent.errors.total",
            description: "Agent错误总数");
        
        _latencyHistogram = _meter.CreateHistogram<double>(
            "agent.request.duration",
            unit: "ms",
            description: "Agent请求时长（毫秒）");
        
        _tokenHistogram = _meter.CreateHistogram<int>(
            "agent.tokens.used",
            unit: "tokens",
            description: "每个请求使用的token数");
        
        _activeRequestsCounter = _meter.CreateUpDownCounter<int>(
            "agent.requests.active",
            description: "活跃Agent请求数");
    }

    /// <summary>
    /// 为Agent执行启动新的追踪span
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
    /// 记录请求
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
    /// 跟踪活跃请求
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

## 第3-4天：结构化日志

### 1. Agent日志器

```csharp
namespace DawningAgents.Core.Observability;

using Microsoft.Extensions.Logging;

/// <summary>
/// Agent结构化日志器
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
            "Agent {AgentName} 开始请求 {RequestId}。输入长度：{InputLength}",
            _agentName, requestId, input.Length);
    }

    public void LogRequestComplete(string requestId, bool success, TimeSpan duration, int? tokensUsed = null)
    {
        if (!_config.EnableLogging) return;
        
        if (success)
        {
            _logger.LogInformation(
                "Agent {AgentName} 完成请求 {RequestId}，耗时 {DurationMs}ms。Token数：{TokensUsed}",
                _agentName, requestId, duration.TotalMilliseconds, tokensUsed ?? 0);
        }
        else
        {
            _logger.LogWarning(
                "Agent {AgentName} 请求 {RequestId} 失败，耗时 {DurationMs}ms",
                _agentName, requestId, duration.TotalMilliseconds);
        }
    }

    public void LogToolCall(string requestId, string toolName, bool success, TimeSpan duration)
    {
        if (!_config.EnableLogging) return;
        
        _logger.LogDebug(
            "Agent {AgentName} 为请求 {RequestId} 调用工具 {ToolName}。成功：{Success}，耗时：{DurationMs}ms",
            _agentName, toolName, requestId, success, duration.TotalMilliseconds);
    }

    public void LogIteration(string requestId, int iteration, string thought)
    {
        if (!_config.EnableLogging) return;
        
        _logger.LogDebug(
            "Agent {AgentName} 请求 {RequestId} 第 {Iteration} 次迭代。思考：{Thought}",
            _agentName, iteration, requestId, thought.Length > 100 ? thought[..100] + "..." : thought);
    }

    public void LogError(string requestId, Exception ex, string context)
    {
        if (!_config.EnableLogging) return;
        
        _logger.LogError(ex,
            "Agent {AgentName} 请求 {RequestId} 错误。上下文：{Context}",
            _agentName, requestId, context);
    }

    public void LogGuardrailTriggered(string requestId, string guardrailName, string action, string? reason = null)
    {
        if (!_config.EnableLogging) return;
        
        _logger.LogWarning(
            "Agent {AgentName} 护栏 {GuardrailName} 在请求 {RequestId} 触发。操作：{Action}，原因：{Reason}",
            _agentName, guardrailName, requestId, action, reason ?? "无");
    }

    public void LogLLMCall(string requestId, string model, int promptTokens, int completionTokens, TimeSpan duration)
    {
        if (!_config.EnableLogging) return;
        
        _logger.LogDebug(
            "Agent {AgentName} 请求 {RequestId} LLM调用。模型：{Model}，提示词：{PromptTokens}，补全：{CompletionTokens}，耗时：{DurationMs}ms",
            _agentName, requestId, model, promptTokens, completionTokens, duration.TotalMilliseconds);
    }
}
```

### 2. 日志上下文与丰富

```csharp
namespace DawningAgents.Core.Observability;

using System.Collections.Concurrent;

/// <summary>
/// 为日志提供上下文信息
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
    /// 推入新的日志上下文作用域
    /// </summary>
    public static LogContext Push()
    {
        var context = new LogContext(_current.Value);
        _current.Value = context;
        return context;
    }

    /// <summary>
    /// 设置上下文属性
    /// </summary>
    public LogContext Set(string key, object value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// 获取包括父上下文的所有属性
    /// </summary>
    public IDictionary<string, object> GetAllProperties()
    {
        var result = new Dictionary<string, object>();
        
        // 首先获取父上下文属性
        if (_parent != null)
        {
            foreach (var (key, value) in _parent.GetAllProperties())
            {
                result[key] = value;
            }
        }
        
        // 用当前上下文覆盖
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
/// 日志上下文扩展
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

## 第5-7天：指标与追踪

### 1. 指标收集器

```csharp
namespace DawningAgents.Core.Observability;

using System.Collections.Concurrent;

/// <summary>
/// 用于开发/测试的内存指标收集器
/// </summary>
public class MetricsCollector
{
    private readonly ConcurrentDictionary<string, CounterMetric> _counters = new();
    private readonly ConcurrentDictionary<string, HistogramMetric> _histograms = new();
    private readonly ConcurrentDictionary<string, GaugeMetric> _gauges = new();

    /// <summary>
    /// 增加计数器
    /// </summary>
    public void IncrementCounter(string name, long value = 1, IDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        var counter = _counters.GetOrAdd(key, _ => new CounterMetric(name, tags));
        counter.Add(value);
    }

    /// <summary>
    /// 记录直方图值
    /// </summary>
    public void RecordHistogram(string name, double value, IDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        var histogram = _histograms.GetOrAdd(key, _ => new HistogramMetric(name, tags));
        histogram.Record(value);
    }

    /// <summary>
    /// 设置仪表值
    /// </summary>
    public void SetGauge(string name, double value, IDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        var gauge = _gauges.GetOrAdd(key, _ => new GaugeMetric(name, tags));
        gauge.Set(value);
    }

    /// <summary>
    /// 获取所有指标快照
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

### 2. 分布式追踪

```csharp
namespace DawningAgents.Core.Observability;

using System.Diagnostics;

/// <summary>
/// 分布式追踪工具
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
    /// 启动新的追踪span
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
    /// 获取当前追踪上下文用于传播
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
    /// 从传播的上下文继续
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
    Internal,  // 内部
    Client,    // 客户端
    Server,    // 服务端
    Producer,  // 生产者
    Consumer   // 消费者
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
    Ok,     // 正常
    Error,  // 错误
    Unset   // 未设置
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

### 3. 可观测Agent包装器

```csharp
namespace DawningAgents.Core.Observability;

using Microsoft.Extensions.Logging;

/// <summary>
/// 用完整可观测性包装Agent
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

        // 启动追踪span
        using var span = _tracer.StartSpan($"{_innerAgent.Name}.Execute", SpanKind.Internal);
        span.SetAttribute("request.id", requestId);
        span.SetAttribute("input.length", context.Input.Length);

        // 设置日志上下文
        using var logContext = LogContext.Push()
            .WithRequestId(requestId)
            .WithAgentName(_innerAgent.Name);

        // 跟踪活跃请求
        using var activeTracker = _telemetry.TrackActiveRequest(_innerAgent.Name);

        _agentLogger.LogRequestStart(requestId, context.Input);

        try
        {
            var response = await _innerAgent.ExecuteAsync(context, cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            // 记录成功指标
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

            _agentLogger.LogError(requestId, ex, "Agent执行失败");

            span.RecordException(ex);
            span.SetStatus(SpanStatus.Error, ex.Message);

            throw;
        }
    }

    /// <summary>
    /// 获取当前指标快照
    /// </summary>
    public MetricsSnapshot GetMetrics() => _metrics.GetSnapshot();
}
```

### 4. 健康检查

```csharp
namespace DawningAgents.Core.Observability;

/// <summary>
/// Agent系统健康检查
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
    Healthy,   // 健康
    Degraded,  // 降级
    Unhealthy  // 不健康
}

/// <summary>
/// LLM提供者健康检查
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
            await _llm.GenerateAsync("你好", maxTokens: 5, cancellationToken: cancellationToken);
            var latency = DateTime.UtcNow - start;

            return new ComponentHealth
            {
                Name = Name,
                Status = latency.TotalSeconds < 5 ? HealthStatus.Healthy : HealthStatus.Degraded,
                Message = $"响应时间：{latency.TotalMilliseconds:F0}ms",
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

## 完整示例

```csharp
// 配置遥测
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

// 创建遥测提供者
var telemetry = new AgentTelemetry(telemetryConfig);
var logger = loggerFactory.CreateLogger<ObservableAgent>();

// 创建可观测Agent
var innerAgent = new ReActAgent(llm, loggerFactory.CreateLogger<ReActAgent>());
var observableAgent = new ObservableAgent(innerAgent, telemetry, logger, telemetryConfig);

// 设置健康检查
var healthCheck = new AgentHealthCheck();
healthCheck.AddProvider(new LLMHealthCheck(llm));

// 带完整可观测性执行
using (LogContext.Push()
    .WithUserId("user-123")
    .WithSessionId("session-456"))
{
    var response = await observableAgent.ExecuteAsync(new AgentContext
    {
        Input = "分析当前市场趋势"
    });

    Console.WriteLine($"响应：{response.Output}");
    Console.WriteLine($"请求ID：{response.Metadata["request_id"]}");
}

// 获取指标
var metrics = observableAgent.GetMetrics();
Console.WriteLine($"\n=== 指标快照 ===");
foreach (var counter in metrics.Counters)
{
    Console.WriteLine($"{counter.Name}：{counter.Value}");
}
foreach (var histogram in metrics.Histograms)
{
    Console.WriteLine($"{histogram.Name}：count={histogram.Count}, p50={histogram.P50:F2}ms, p99={histogram.P99:F2}ms");
}

// 检查健康
var health = await healthCheck.CheckHealthAsync();
Console.WriteLine($"\n=== 健康检查 ===");
Console.WriteLine($"状态：{health.Status}");
foreach (var component in health.Components)
{
    Console.WriteLine($"  {component.Name}：{component.Status} - {component.Message}");
}
```

---

## 总结

### 第11周交付物

```
src/DawningAgents.Core/
└── Observability/
    ├── TelemetryConfig.cs        # 配置
    ├── AgentTelemetry.cs         # 指标/追踪提供者
    ├── AgentLogger.cs            # 结构化日志
    ├── LogContext.cs             # 日志上下文/丰富
    ├── MetricsCollector.cs       # 内存指标
    ├── DistributedTracer.cs      # 分布式追踪
    ├── ObservableAgent.cs        # 可观测包装器
    └── HealthCheck.cs            # 健康检查
```

### 可观测性组件

| 组件 | 用途 |
|------|------|
| **日志** | 结构化事件记录 |
| **指标** | 定量测量 |
| **追踪** | 请求流跟踪 |
| **健康检查** | 系统健康监控 |

### 关键指标

| 指标 | 类型 | 描述 |
|------|------|------|
| `agent.requests.total` | 计数器 | 总请求数 |
| `agent.errors.total` | 计数器 | 总错误数 |
| `agent.request.duration` | 直方图 | 延迟（毫秒） |
| `agent.tokens.used` | 直方图 | Token消耗 |
| `agent.requests.active` | 仪表 | 活跃请求数 |

### 下一步：第12周

第12周将涵盖部署与扩展：
- 容器化
- 配置管理
- 扩展策略
- 生产部署
