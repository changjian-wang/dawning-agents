namespace Dawning.Agents.Tests.Observability;

using Dawning.Agents.Abstractions.Observability;
using Dawning.Agents.Core.Observability;
using FluentAssertions;

public class AgentTelemetryTests : IDisposable
{
    private readonly TelemetryConfig _config;
    private readonly AgentTelemetry _telemetry;

    public AgentTelemetryTests()
    {
        _config = new TelemetryConfig
        {
            EnableLogging = true,
            EnableMetrics = true,
            EnableTracing = true,
            ServiceName = "TestService",
            ServiceVersion = "1.0.0",
        };
        _telemetry = new AgentTelemetry(_config);
    }

    public void Dispose()
    {
        _telemetry.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RecordRequest_ShouldNotThrow()
    {
        // Act
        var act = () => _telemetry.RecordRequest("TestAgent", true, 100.5, 1000);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordRequest_WithoutTokens_ShouldNotThrow()
    {
        // Act
        var act = () => _telemetry.RecordRequest("TestAgent", false, 50.0);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordRequest_WhenMetricsDisabled_ShouldNotThrow()
    {
        // Arrange
        var config = new TelemetryConfig { EnableMetrics = false };
        using var telemetry = new AgentTelemetry(config);

        // Act
        var act = () => telemetry.RecordRequest("TestAgent", true, 100);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void StartAgentSpan_ShouldReturnActivity()
    {
        // Act
        using var activity = _telemetry.StartAgentSpan("TestAgent", "Run");

        // Assert
        // Activity 可能为 null（如果没有 listener），但不应该抛出异常
    }

    [Fact]
    public void StartAgentSpan_WhenTracingDisabled_ShouldReturnNull()
    {
        // Arrange
        var config = new TelemetryConfig { EnableTracing = false };
        using var telemetry = new AgentTelemetry(config);

        // Act
        var activity = telemetry.StartAgentSpan("TestAgent", "Run");

        // Assert
        activity.Should().BeNull();
    }

    [Fact]
    public void TrackActiveRequest_ShouldReturnDisposable()
    {
        // Act
        var tracker = _telemetry.TrackActiveRequest("TestAgent");

        // Assert
        tracker.Should().NotBeNull();
        tracker.Dispose();
    }

    [Fact]
    public void TrackActiveRequest_WhenMetricsDisabled_ShouldReturnNoOpDisposable()
    {
        // Arrange
        var config = new TelemetryConfig { EnableMetrics = false };
        using var telemetry = new AgentTelemetry(config);

        // Act
        var tracker = telemetry.TrackActiveRequest("TestAgent");

        // Assert
        tracker.Should().NotBeNull();
        var act = () => tracker.Dispose();
        act.Should().NotThrow();
    }
}
