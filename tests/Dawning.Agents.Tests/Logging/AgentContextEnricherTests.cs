using Dawning.Agents.Abstractions.Logging;
using Dawning.Agents.Core.Logging;
using FluentAssertions;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Dawning.Agents.Tests.Logging;

/// <summary>
/// AgentContextEnricher 测试
/// </summary>
public class AgentContextEnricherTests
{
    private readonly AgentContextEnricher _enricher = new();

    [Fact]
    public void Enrich_ShouldDoNothing_WhenNoContext()
    {
        // Arrange
        var logEvent = CreateLogEvent("Test message");
        var propertyFactory = new LogEventPropertyFactory();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().BeEmpty();
    }

    [Fact]
    public void Enrich_ShouldAddAgentName_WhenInContext()
    {
        // Arrange
        using var scope = AgentLogContext.BeginScope(agentName: "TestAgent");
        var logEvent = CreateLogEvent("Test message");
        var propertyFactory = new LogEventPropertyFactory();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().ContainKey("AgentName");
        GetPropertyValue(logEvent, "AgentName").Should().Be("TestAgent");
    }

    [Fact]
    public void Enrich_ShouldAddRequestId_WhenInContext()
    {
        // Arrange
        using var scope = AgentLogContext.BeginScope(requestId: "req-123");
        var logEvent = CreateLogEvent("Test message");
        var propertyFactory = new LogEventPropertyFactory();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().ContainKey("RequestId");
        GetPropertyValue(logEvent, "RequestId").Should().Be("req-123");
    }

    [Fact]
    public void Enrich_ShouldAddSessionId_WhenInContext()
    {
        // Arrange
        using var scope = AgentLogContext.BeginScope(sessionId: "sess-456");
        var logEvent = CreateLogEvent("Test message");
        var propertyFactory = new LogEventPropertyFactory();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().ContainKey("SessionId");
        GetPropertyValue(logEvent, "SessionId").Should().Be("sess-456");
    }

    [Fact]
    public void Enrich_ShouldAddUserId_WhenInContext()
    {
        // Arrange
        using var scope = AgentLogContext.BeginScope(userId: "user-789");
        var logEvent = CreateLogEvent("Test message");
        var propertyFactory = new LogEventPropertyFactory();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().ContainKey("UserId");
        GetPropertyValue(logEvent, "UserId").Should().Be("user-789");
    }

    [Fact]
    public void Enrich_ShouldAddToolName_WhenSet()
    {
        // Arrange
        using var scope = AgentLogContext.BeginScope(agentName: "TestAgent");
        AgentLogContext.SetTool("TestTool");
        var logEvent = CreateLogEvent("Test message");
        var propertyFactory = new LogEventPropertyFactory();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().ContainKey("ToolName");
        GetPropertyValue(logEvent, "ToolName").Should().Be("TestTool");
    }

    [Fact]
    public void Enrich_ShouldAddStepNumber_WhenSet()
    {
        // Arrange
        using var scope = AgentLogContext.BeginScope(agentName: "TestAgent");
        AgentLogContext.SetStep(3);
        var logEvent = CreateLogEvent("Test message");
        var propertyFactory = new LogEventPropertyFactory();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().ContainKey("StepNumber");
        var stepValue = logEvent.Properties["StepNumber"];
        stepValue.Should().BeOfType<ScalarValue>();
        ((ScalarValue)stepValue).Value.Should().Be(3);
    }

    [Fact]
    public void Enrich_ShouldAddAllProperties_WhenAllSet()
    {
        // Arrange
        using var scope = AgentLogContext.BeginScope(
            agentName: "TestAgent",
            requestId: "req-123",
            sessionId: "sess-456",
            userId: "user-789"
        );
        AgentLogContext.SetTool("TestTool");
        AgentLogContext.SetStep(5);

        var logEvent = CreateLogEvent("Test message");
        var propertyFactory = new LogEventPropertyFactory();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().HaveCount(6);
        logEvent.Properties.Should().ContainKey("AgentName");
        logEvent.Properties.Should().ContainKey("RequestId");
        logEvent.Properties.Should().ContainKey("SessionId");
        logEvent.Properties.Should().ContainKey("UserId");
        logEvent.Properties.Should().ContainKey("ToolName");
        logEvent.Properties.Should().ContainKey("StepNumber");
    }

    [Fact]
    public void Enrich_ShouldNotOverwriteExistingProperties()
    {
        // Arrange
        using var scope = AgentLogContext.BeginScope(agentName: "TestAgent");
        var logEvent = CreateLogEvent("Test message");
        var propertyFactory = new LogEventPropertyFactory();

        // Pre-add a property
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("AgentName", "ExistingAgent"));

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        GetPropertyValue(logEvent, "AgentName").Should().Be("ExistingAgent");
    }

    private static LogEvent CreateLogEvent(string message)
    {
        var parser = new MessageTemplateParser();
        var template = parser.Parse(message);
        return new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            null,
            template,
            Array.Empty<LogEventProperty>()
        );
    }

    private static string? GetPropertyValue(LogEvent logEvent, string name)
    {
        if (logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue scalar)
        {
            return scalar.Value?.ToString();
        }
        return null;
    }

    private sealed class LogEventPropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(
            string name,
            object? value,
            bool destructureObjects = false
        )
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
}
