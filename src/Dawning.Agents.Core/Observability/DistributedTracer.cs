namespace Dawning.Agents.Core.Observability;

using System.Diagnostics;
using Dawning.Agents.Abstractions.Observability;

/// <summary>
/// Distributed tracer.
/// </summary>
public sealed class DistributedTracer : IDisposable
{
    private readonly ActivitySource _source;
    private readonly TelemetryConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedTracer"/> class.
    /// </summary>
    public DistributedTracer(TelemetryConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _source = new ActivitySource(config.ServiceName, config.ServiceVersion);
    }

    /// <summary>
    /// Starts a new trace span.
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
    /// Gets the current trace context for propagation.
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
    /// Starts a span from a propagated trace context.
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

        ActivityContext parentContext;
        try
        {
            var traceId = ActivityTraceId.CreateFromString(context.TraceId.AsSpan());
            var spanId = ActivitySpanId.CreateFromString(context.SpanId.AsSpan());
            var traceFlags = Enum.TryParse<ActivityTraceFlags>(context.TraceFlags, out var flags)
                ? flags
                : ActivityTraceFlags.None;

            parentContext = new ActivityContext(traceId, spanId, traceFlags);
        }
        catch (FormatException)
        {
            // Malformed trace context — fall back to root span
            return StartSpan(name, kind);
        }

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

    /// <summary>
    /// Releases the <see cref="ActivitySource"/> resources.
    /// </summary>
    public void Dispose()
    {
        _source.Dispose();
    }
}

/// <summary>
/// Span backed by an <see cref="Activity"/>.
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
    }
}

/// <summary>
/// No-op span used when tracing is disabled.
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
