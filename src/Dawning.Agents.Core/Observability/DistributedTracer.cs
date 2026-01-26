namespace Dawning.Agents.Core.Observability;

using System.Diagnostics;
using Dawning.Agents.Abstractions.Observability;

/// <summary>
/// 分布式追踪器
/// </summary>
public sealed class DistributedTracer
{
    private readonly ActivitySource _source;
    private readonly TelemetryConfig _config;

    /// <summary>
    /// 创建分布式追踪器
    /// </summary>
    public DistributedTracer(TelemetryConfig config)
    {
        _config = config;
        _source = new ActivitySource(config.ServiceName, config.ServiceVersion);
    }

    /// <summary>
    /// 启动新的追踪 span
    /// </summary>
    public ITraceSpan StartSpan(string name, SpanKind kind = SpanKind.Internal)
    {
        if (!_config.EnableTracing)
        {
            return new NoOpSpan();
        }

        var activityKind = kind switch
        {
            SpanKind.Client => ActivityKind.Client,
            SpanKind.Server => ActivityKind.Server,
            SpanKind.Producer => ActivityKind.Producer,
            SpanKind.Consumer => ActivityKind.Consumer,
            _ => ActivityKind.Internal,
        };

        var activity = _source.StartActivity(name, activityKind);
        if (activity == null)
        {
            return new NoOpSpan();
        }

        return new ActivitySpan(activity);
    }

    /// <summary>
    /// 获取当前追踪上下文用于传播
    /// </summary>
    public TraceContext? GetCurrentContext()
    {
        var activity = Activity.Current;
        if (activity == null)
        {
            return null;
        }

        return new TraceContext
        {
            TraceId = activity.TraceId.ToString(),
            SpanId = activity.SpanId.ToString(),
            TraceFlags = activity.ActivityTraceFlags.ToString(),
        };
    }

    /// <summary>
    /// 从传播的上下文继续
    /// </summary>
    public ITraceSpan StartSpanFromContext(
        string name,
        TraceContext context,
        SpanKind kind = SpanKind.Internal
    )
    {
        if (!_config.EnableTracing)
        {
            return new NoOpSpan();
        }

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
                _ => ActivityKind.Internal,
            },
            parentContext
        );

        if (activity == null)
        {
            return new NoOpSpan();
        }

        return new ActivitySpan(activity);
    }
}

/// <summary>
/// Activity 包装的 Span
/// </summary>
internal class ActivitySpan : ITraceSpan
{
    private readonly Activity _activity;

    public ActivitySpan(Activity activity)
    {
        _activity = activity;
    }

    /// <inheritdoc />
    public string SpanId => _activity.SpanId.ToString();

    /// <inheritdoc />
    public void SetAttribute(string key, object value)
    {
        _activity.SetTag(key, value);
    }

    /// <inheritdoc />
    public void SetStatus(SpanStatus status, string? description = null)
    {
        _activity.SetStatus(
            status switch
            {
                SpanStatus.Ok => ActivityStatusCode.Ok,
                SpanStatus.Error => ActivityStatusCode.Error,
                _ => ActivityStatusCode.Unset,
            },
            description
        );
    }

    /// <inheritdoc />
    public void RecordException(Exception ex)
    {
        _activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        _activity.SetTag("exception.type", ex.GetType().FullName);
        _activity.SetTag("exception.message", ex.Message);
        _activity.SetTag("exception.stacktrace", ex.StackTrace);
    }

    /// <inheritdoc />
    public ITraceSpan StartChildSpan(string name)
    {
        var childActivity = _activity.Source.StartActivity(
            name,
            ActivityKind.Internal,
            _activity.Context
        );
        if (childActivity == null)
        {
            return new NoOpSpan();
        }

        return new ActivitySpan(childActivity);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _activity.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 空操作 Span（追踪禁用时使用）
/// </summary>
internal class NoOpSpan : ITraceSpan
{
    /// <inheritdoc />
    public string SpanId => string.Empty;

    /// <inheritdoc />
    public void SetAttribute(string key, object value) { }

    /// <inheritdoc />
    public void SetStatus(SpanStatus status, string? description = null) { }

    /// <inheritdoc />
    public void RecordException(Exception ex) { }

    /// <inheritdoc />
    public ITraceSpan StartChildSpan(string name) => this;

    /// <inheritdoc />
    public void Dispose() { }
}
