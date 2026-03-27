using Dawning.Agents.Abstractions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Dawning.Agents.Serilog;

/// <summary>
/// Agent log context enricher that adds <see cref="AgentLogContext"/> information to log events.
/// </summary>
public sealed class AgentContextEnricher : ILogEventEnricher
{
    /// <summary>
    /// Enriches the log event with agent context properties.
    /// </summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var context = AgentLogContext.Current;
        if (context == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(context.AgentName))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("AgentName", context.AgentName)
            );
        }

        if (!string.IsNullOrEmpty(context.RequestId))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("RequestId", context.RequestId)
            );
        }

        if (!string.IsNullOrEmpty(context.SessionId))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("SessionId", context.SessionId)
            );
        }

        if (!string.IsNullOrEmpty(context.UserId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", context.UserId));
        }

        if (!string.IsNullOrEmpty(context.ToolName))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("ToolName", context.ToolName)
            );
        }

        if (context.StepNumber.HasValue)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("StepNumber", context.StepNumber.Value)
            );
        }
    }
}
