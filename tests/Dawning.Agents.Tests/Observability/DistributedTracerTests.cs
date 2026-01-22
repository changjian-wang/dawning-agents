namespace Dawning.Agents.Tests.Observability;

using Dawning.Agents.Abstractions.Observability;
using Dawning.Agents.Core.Observability;
using FluentAssertions;

public class DistributedTracerTests
{
    [Fact]
    public void StartSpan_WhenEnabled_ShouldReturnSpan()
    {
        // Arrange
        var config = new TelemetryConfig { EnableTracing = true };
        var tracer = new DistributedTracer(config);

        // Act
        using var span = tracer.StartSpan("TestSpan");

        // Assert
        // Note: 可能返回 NoOpSpan 如果没有 listener
        span.Should().NotBeNull();
    }

    [Fact]
    public void StartSpan_WhenDisabled_ShouldReturnNoOpSpan()
    {
        // Arrange
        var config = new TelemetryConfig { EnableTracing = false };
        var tracer = new DistributedTracer(config);

        // Act
        using var span = tracer.StartSpan("TestSpan");

        // Assert
        span.SpanId.Should().BeEmpty();
    }

    [Theory]
    [InlineData(SpanKind.Internal)]
    [InlineData(SpanKind.Client)]
    [InlineData(SpanKind.Server)]
    [InlineData(SpanKind.Producer)]
    [InlineData(SpanKind.Consumer)]
    public void StartSpan_WithDifferentKinds_ShouldNotThrow(SpanKind kind)
    {
        // Arrange
        var config = new TelemetryConfig { EnableTracing = true };
        var tracer = new DistributedTracer(config);

        // Act
        var act = () =>
        {
            using var span = tracer.StartSpan("TestSpan", kind);
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetCurrentContext_WithNoActivity_ShouldReturnNull()
    {
        // Arrange
        var config = new TelemetryConfig { EnableTracing = true };
        var tracer = new DistributedTracer(config);

        // Act
        var context = tracer.GetCurrentContext();

        // Assert
        context.Should().BeNull();
    }

    [Fact]
    public void NoOpSpan_ShouldImplementAllMethods()
    {
        // Arrange
        var config = new TelemetryConfig { EnableTracing = false };
        var tracer = new DistributedTracer(config);

        // Act
        using var span = tracer.StartSpan("Test");

        // Assert - 所有方法应该不抛出异常
        span.SpanId.Should().BeEmpty();

        var act = () =>
        {
            span.SetAttribute("key", "value");
            span.SetStatus(SpanStatus.Ok, "ok");
            span.RecordException(new Exception("test"));
            using var child = span.StartChildSpan("child");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void StartSpanFromContext_WhenDisabled_ShouldReturnNoOpSpan()
    {
        // Arrange
        var config = new TelemetryConfig { EnableTracing = false };
        var tracer = new DistributedTracer(config);
        var context = new TraceContext
        {
            TraceId = "00000000000000000000000000000001",
            SpanId = "0000000000000001",
            TraceFlags = "None",
        };

        // Act
        using var span = tracer.StartSpanFromContext("Test", context);

        // Assert
        span.SpanId.Should().BeEmpty();
    }
}
