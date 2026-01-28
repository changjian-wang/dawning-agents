using Serilog.Core;
using Serilog.Events;

namespace Dawning.Agents.Core.Logging;

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

        if (!string.IsNullOrEmpty(activity.TraceId.ToString()))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString())
            );
        }

        if (!string.IsNullOrEmpty(activity.SpanId.ToString()))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString())
            );
        }

        if (!string.IsNullOrEmpty(activity.ParentSpanId.ToString()))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("ParentSpanId", activity.ParentSpanId.ToString())
            );
        }
    }
}
