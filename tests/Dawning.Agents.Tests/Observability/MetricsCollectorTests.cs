namespace Dawning.Agents.Tests.Observability;

using Dawning.Agents.Abstractions.Observability;
using Dawning.Agents.Core.Observability;
using FluentAssertions;

public class MetricsCollectorTests
{
    private readonly MetricsCollector _collector = new();

    [Fact]
    public void IncrementCounter_ShouldIncrementValue()
    {
        // Act
        _collector.IncrementCounter("test.counter");
        _collector.IncrementCounter("test.counter");
        _collector.IncrementCounter("test.counter", 5);

        // Assert
        var value = _collector.GetCounter("test.counter");
        value.Should().Be(7);
    }

    [Fact]
    public void IncrementCounter_WithTags_ShouldTrackSeparately()
    {
        // Arrange
        var tags1 = new Dictionary<string, string> { ["env"] = "dev" };
        var tags2 = new Dictionary<string, string> { ["env"] = "prod" };

        // Act
        _collector.IncrementCounter("test.counter", 1, tags1);
        _collector.IncrementCounter("test.counter", 2, tags2);
        _collector.IncrementCounter("test.counter", 3, tags1);

        // Assert
        _collector.GetCounter("test.counter", tags1).Should().Be(4);
        _collector.GetCounter("test.counter", tags2).Should().Be(2);
    }

    [Fact]
    public void RecordHistogram_ShouldTrackValues()
    {
        // Act
        _collector.RecordHistogram("test.latency", 10);
        _collector.RecordHistogram("test.latency", 20);
        _collector.RecordHistogram("test.latency", 30);
        _collector.RecordHistogram("test.latency", 40);
        _collector.RecordHistogram("test.latency", 50);

        // Assert
        var snapshot = _collector.GetSnapshot();
        var histogram = snapshot.Histograms.FirstOrDefault(h => h.Name == "test.latency");
        histogram.Should().NotBeNull();
        histogram!.Count.Should().Be(5);
        histogram.Sum.Should().Be(150);
        histogram.Min.Should().Be(10);
        histogram.Max.Should().Be(50);
        histogram.P50.Should().Be(30); // Middle value
    }

    [Fact]
    public void SetGauge_ShouldSetValue()
    {
        // Act
        _collector.SetGauge("test.gauge", 100);
        _collector.SetGauge("test.gauge", 200);

        // Assert
        var value = _collector.GetGauge("test.gauge");
        value.Should().Be(200);
    }

    [Fact]
    public void GetSnapshot_ShouldReturnAllMetrics()
    {
        // Arrange
        _collector.IncrementCounter("counter1");
        _collector.IncrementCounter("counter2", 5);
        _collector.RecordHistogram("histogram1", 10);
        _collector.SetGauge("gauge1", 100);

        // Act
        var snapshot = _collector.GetSnapshot();

        // Assert
        snapshot.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        snapshot.Counters.Should().HaveCount(2);
        snapshot.Histograms.Should().HaveCount(1);
        snapshot.Gauges.Should().HaveCount(1);
    }

    [Fact]
    public void Clear_ShouldRemoveAllMetrics()
    {
        // Arrange
        _collector.IncrementCounter("counter1");
        _collector.RecordHistogram("histogram1", 10);
        _collector.SetGauge("gauge1", 100);

        // Act
        _collector.Clear();
        var snapshot = _collector.GetSnapshot();

        // Assert
        snapshot.Counters.Should().BeEmpty();
        snapshot.Histograms.Should().BeEmpty();
        snapshot.Gauges.Should().BeEmpty();
    }

    [Fact]
    public void GetCounter_WithNonExistent_ShouldReturnNull()
    {
        // Act
        var value = _collector.GetCounter("nonexistent");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void GetGauge_WithNonExistent_ShouldReturnNull()
    {
        // Act
        var value = _collector.GetGauge("nonexistent");

        // Assert
        value.Should().BeNull();
    }
}
