namespace Dawning.Agents.Tests.Observability;

using Dawning.Agents.Abstractions.Observability;
using FluentAssertions;

public class MetricsModelsTests
{
    [Fact]
    public void MetricData_ShouldHaveCorrectDefaults()
    {
        // Act
        var data = new MetricData { Name = "test", Type = "counter" };

        // Assert
        data.Name.Should().Be("test");
        data.Type.Should().Be("counter");
        data.Value.Should().Be(0);
        data.Count.Should().Be(0);
        data.Sum.Should().Be(0);
        data.Min.Should().Be(0);
        data.Max.Should().Be(0);
        data.P50.Should().Be(0);
        data.P95.Should().Be(0);
        data.P99.Should().Be(0);
        data.Tags.Should().BeNull();
    }

    [Fact]
    public void MetricData_ShouldStoreAllProperties()
    {
        // Arrange
        var tags = new Dictionary<string, string> { ["env"] = "test" };

        // Act
        var data = new MetricData
        {
            Name = "test.metric",
            Type = "histogram",
            Value = 100,
            Count = 10,
            Sum = 500,
            Min = 10,
            Max = 100,
            P50 = 50,
            P95 = 95,
            P99 = 99,
            Tags = tags,
        };

        // Assert
        data.Name.Should().Be("test.metric");
        data.Type.Should().Be("histogram");
        data.Value.Should().Be(100);
        data.Count.Should().Be(10);
        data.Sum.Should().Be(500);
        data.Min.Should().Be(10);
        data.Max.Should().Be(100);
        data.P50.Should().Be(50);
        data.P95.Should().Be(95);
        data.P99.Should().Be(99);
        data.Tags.Should().BeEquivalentTo(tags);
    }

    [Fact]
    public void MetricsSnapshot_ShouldHaveCorrectDefaults()
    {
        // Act
        var snapshot = new MetricsSnapshot { Timestamp = DateTime.UtcNow };

        // Assert
        snapshot.Counters.Should().BeEmpty();
        snapshot.Histograms.Should().BeEmpty();
        snapshot.Gauges.Should().BeEmpty();
    }

    [Fact]
    public void MetricsSnapshot_ShouldStoreAllMetrics()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var counters = new List<MetricData>
        {
            new() { Name = "c1", Type = "counter" },
        };
        var histograms = new List<MetricData>
        {
            new() { Name = "h1", Type = "histogram" },
        };
        var gauges = new List<MetricData>
        {
            new() { Name = "g1", Type = "gauge" },
        };

        // Act
        var snapshot = new MetricsSnapshot
        {
            Timestamp = timestamp,
            Counters = counters,
            Histograms = histograms,
            Gauges = gauges,
        };

        // Assert
        snapshot.Timestamp.Should().Be(timestamp);
        snapshot.Counters.Should().HaveCount(1);
        snapshot.Histograms.Should().HaveCount(1);
        snapshot.Gauges.Should().HaveCount(1);
    }
}
