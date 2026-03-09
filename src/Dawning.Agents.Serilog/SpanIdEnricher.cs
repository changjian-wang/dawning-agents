using Serilog.Core;
using Serilog.Events;

namespace Dawning.Agents.Serilog;

/// <summary>
/// Span ID Enricher - 添加 OpenTelemetry Span ID 到日志
/// </summary>
public sealed class SpanIdEnricher : ILogEventEnricher
{
    /// <summary>
    /// 丰富日志事件
    /// </summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = System.Diagnostics.Activity.Current;
        if (activity == null)
        {
            return;
        }

        var traceId = activity.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));
        }

        var spanId = activity.SpanId.ToString();
        if (!string.IsNullOrEmpty(spanId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", spanId));
        }

        var parentSpanId = activity.ParentSpanId.ToString();
        if (!string.IsNullOrEmpty(parentSpanId))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("ParentSpanId", parentSpanId)
            );
        }
    }
}
