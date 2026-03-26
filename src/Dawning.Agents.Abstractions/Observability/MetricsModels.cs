namespace Dawning.Agents.Abstractions.Observability;

/// <summary>
/// Represents a single metric data point.
/// </summary>
public record MetricData
{
    /// <summary>
    /// Gets the metric name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the metric type (counter, histogram, gauge).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the value (used for counters and gauges).
    /// </summary>
    public double Value { get; init; }

    /// <summary>
    /// Gets the count (used for histograms).
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Gets the sum (used for histograms).
    /// </summary>
    public double Sum { get; init; }

    /// <summary>
    /// Gets the minimum value.
    /// </summary>
    public double Min { get; init; }

    /// <summary>
    /// Gets the maximum value.
    /// </summary>
    public double Max { get; init; }

    /// <summary>
    /// Gets the 50th percentile.
    /// </summary>
    public double P50 { get; init; }

    /// <summary>
    /// Gets the 95th percentile.
    /// </summary>
    public double P95 { get; init; }

    /// <summary>
    /// Gets the 99th percentile.
    /// </summary>
    public double P99 { get; init; }

    /// <summary>
    /// Gets the optional tags associated with this metric.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

/// <summary>
/// Represents a point-in-time snapshot of all metrics.
/// </summary>
public record MetricsSnapshot
{
    /// <summary>
    /// Gets the timestamp of the snapshot.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the counter metrics.
    /// </summary>
    public IReadOnlyList<MetricData> Counters { get; init; } = [];

    /// <summary>
    /// Gets the histogram metrics.
    /// </summary>
    public IReadOnlyList<MetricData> Histograms { get; init; } = [];

    /// <summary>
    /// Gets the gauge metrics.
    /// </summary>
    public IReadOnlyList<MetricData> Gauges { get; init; } = [];
}
