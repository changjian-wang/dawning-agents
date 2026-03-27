namespace Dawning.Agents.Core.Observability;

using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Observability;

/// <summary>
/// In-memory metrics collector for development and testing.
/// </summary>
public sealed class MetricsCollector
{
    private readonly ConcurrentDictionary<string, CounterMetric> _counters = new();
    private readonly ConcurrentDictionary<string, HistogramMetric> _histograms = new();
    private readonly ConcurrentDictionary<string, GaugeMetric> _gauges = new();

    /// <summary>
    /// Increments a counter metric.
    /// </summary>
    public void IncrementCounter(
        string name,
        long value = 1,
        IReadOnlyDictionary<string, string>? tags = null
    )
    {
        var key = GetMetricKey(name, tags);
        var counter = _counters.GetOrAdd(key, _ => new CounterMetric(name, tags));
        counter.Add(value);
    }

    /// <summary>
    /// Records a histogram value.
    /// </summary>
    public void RecordHistogram(
        string name,
        double value,
        IReadOnlyDictionary<string, string>? tags = null
    )
    {
        var key = GetMetricKey(name, tags);
        var histogram = _histograms.GetOrAdd(key, _ => new HistogramMetric(name, tags));
        histogram.Record(value);
    }

    /// <summary>
    /// Sets a gauge value.
    /// </summary>
    public void SetGauge(
        string name,
        double value,
        IReadOnlyDictionary<string, string>? tags = null
    )
    {
        var key = GetMetricKey(name, tags);
        var gauge = _gauges.GetOrAdd(key, _ => new GaugeMetric(name, tags));
        gauge.Set(value);
    }

    /// <summary>
    /// Gets the current counter value.
    /// </summary>
    public long? GetCounter(string name, IReadOnlyDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        return _counters.TryGetValue(key, out var counter) ? counter.Value : null;
    }

    /// <summary>
    /// Gets the current gauge value.
    /// </summary>
    public double? GetGauge(string name, IReadOnlyDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        return _gauges.TryGetValue(key, out var gauge) ? gauge.Value : null;
    }

    /// <summary>
    /// Gets a snapshot of all metrics.
    /// </summary>
    public MetricsSnapshot GetSnapshot()
    {
        return new MetricsSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Counters = _counters.Values.Select(c => c.ToData()).ToList(),
            Histograms = _histograms.Values.Select(h => h.ToData()).ToList(),
            Gauges = _gauges.Values.Select(g => g.ToData()).ToList(),
        };
    }

    /// <summary>
    /// Clears all metrics.
    /// </summary>
    public void Clear()
    {
        _counters.Clear();
        _histograms.Clear();
        _gauges.Clear();
    }

    private static string GetMetricKey(string name, IReadOnlyDictionary<string, string>? tags)
    {
        if (tags == null || tags.Count == 0)
        {
            return name;
        }

        var tagString = string.Join(
            ",",
            tags.OrderBy(t => t.Key).Select(t => $"{t.Key}={t.Value}")
        );
        return $"{name}|{tagString}";
    }
}

/// <summary>
/// Counter metric.
/// </summary>
public sealed class CounterMetric
{
    private long _value;

    /// <summary>
    /// The metric name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The metric tags.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; }

    /// <summary>
    /// The current value.
    /// </summary>
    public long Value => _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterMetric"/> class.
    /// </summary>
    public CounterMetric(string name, IReadOnlyDictionary<string, string>? tags)
    {
        Name = name;
        Tags = tags;
    }

    /// <summary>
    /// Adds the specified value.
    /// </summary>
    public void Add(long value) => Interlocked.Add(ref _value, value);

    /// <summary>
    /// Converts to a <see cref="MetricData"/> instance.
    /// </summary>
    public MetricData ToData() =>
        new()
        {
            Name = Name,
            Type = "counter",
            Value = _value,
            Tags = Tags,
        };
}

/// <summary>
/// Histogram metric.
/// </summary>
public sealed class HistogramMetric
{
    private const int _maxValues = 10_000;
    private readonly double[] _values = new double[_maxValues];
    private readonly Lock _lock = new();
    private int _count;
    private int _writeIndex;

    /// <summary>
    /// The metric name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The metric tags.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HistogramMetric"/> class.
    /// </summary>
    public HistogramMetric(string name, IReadOnlyDictionary<string, string>? tags)
    {
        Name = name;
        Tags = tags;
    }

    /// <summary>
    /// Records a value.
    /// </summary>
    public void Record(double value)
    {
        lock (_lock)
        {
            _values[_writeIndex] = value;
            _writeIndex = (_writeIndex + 1) % _maxValues;
            if (_count < _maxValues)
            {
                _count++;
            }
        }
    }

    /// <summary>
    /// Converts to a <see cref="MetricData"/> instance.
    /// </summary>
    public MetricData ToData()
    {
        lock (_lock)
        {
            var snapshot = new double[_count];
            Array.Copy(_values, 0, snapshot, 0, _count);
            Array.Sort(snapshot);
            return new MetricData
            {
                Name = Name,
                Type = "histogram",
                Count = snapshot.Length,
                Sum = snapshot.Sum(),
                Min = snapshot.Length > 0 ? snapshot[0] : 0,
                Max = snapshot.Length > 0 ? snapshot[^1] : 0,
                P50 = GetPercentile(snapshot, 0.5),
                P95 = GetPercentile(snapshot, 0.95),
                P99 = GetPercentile(snapshot, 0.99),
                Tags = Tags,
            };
        }
    }

    private static double GetPercentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }

        var clamped = Math.Clamp(percentile, 0.0, 1.0);
        var index = (int)(clamped * (sorted.Length - 1));
        return sorted[index];
    }
}

/// <summary>
/// Gauge metric.
/// </summary>
public sealed class GaugeMetric
{
    private double _value;

    /// <summary>
    /// The metric name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The metric tags.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; }

    /// <summary>
    /// The current value.
    /// </summary>
    public double Value => _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="GaugeMetric"/> class.
    /// </summary>
    public GaugeMetric(string name, IReadOnlyDictionary<string, string>? tags)
    {
        Name = name;
        Tags = tags;
    }

    /// <summary>
    /// Sets the value.
    /// </summary>
    public void Set(double value) => Interlocked.Exchange(ref _value, value);

    /// <summary>
    /// Converts to a <see cref="MetricData"/> instance.
    /// </summary>
    public MetricData ToData() =>
        new()
        {
            Name = Name,
            Type = "gauge",
            Value = _value,
            Tags = Tags,
        };
}
