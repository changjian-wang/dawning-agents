namespace Dawning.Agents.Tests.Observability;

using Dawning.Agents.Abstractions.Observability;
using FluentAssertions;

public class TracingModelsTests
{
    [Fact]
    public void TraceContext_ShouldStoreValues()
    {
        // Act
        var context = new TraceContext
        {
            TraceId = "trace-123",
            SpanId = "span-456",
            TraceFlags = "Recorded",
        };

        // Assert
        context.TraceId.Should().Be("trace-123");
        context.SpanId.Should().Be("span-456");
        context.TraceFlags.Should().Be("Recorded");
    }

    [Theory]
    [InlineData(SpanKind.Internal, 0)]
    [InlineData(SpanKind.Client, 1)]
    [InlineData(SpanKind.Server, 2)]
    [InlineData(SpanKind.Producer, 3)]
    [InlineData(SpanKind.Consumer, 4)]
    public void SpanKind_ShouldHaveCorrectValues(SpanKind kind, int expectedValue)
    {
        // Assert
        ((int)kind)
            .Should()
            .Be(expectedValue);
    }

    [Theory]
    [InlineData(SpanStatus.Unset, 0)]
    [InlineData(SpanStatus.Ok, 1)]
    [InlineData(SpanStatus.Error, 2)]
    public void SpanStatus_ShouldHaveCorrectValues(SpanStatus status, int expectedValue)
    {
        // Assert
        ((int)status)
            .Should()
            .Be(expectedValue);
    }
}
