namespace Dawning.Agents.Core.Observability;

using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Observability;

/// <summary>
/// 用于开发/测试的内存指标收集器
/// </summary>
public sealed class MetricsCollector
{
    private readonly ConcurrentDictionary<string, CounterMetric> _counters = new();
    private readonly ConcurrentDictionary<string, HistogramMetric> _histograms = new();
    private readonly ConcurrentDictionary<string, GaugeMetric> _gauges = new();

    /// <summary>
    /// 增加计数器
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
    /// 记录直方图值
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
    /// 设置仪表值
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
    /// 获取计数器值
    /// </summary>
    public long? GetCounter(string name, IReadOnlyDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        return _counters.TryGetValue(key, out var counter) ? counter.Value : null;
    }

    /// <summary>
    /// 获取仪表值
    /// </summary>
    public double? GetGauge(string name, IReadOnlyDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        return _gauges.TryGetValue(key, out var gauge) ? gauge.Value : null;
    }

    /// <summary>
    /// 获取所有指标快照
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
    /// 清除所有指标
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
/// 计数器指标
/// </summary>
public sealed class CounterMetric
{
    private long _value;

    /// <summary>
    /// 指标名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 标签
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; }

    /// <summary>
    /// 当前值
    /// </summary>
    public long Value => _value;

    /// <summary>
    /// 创建计数器
    /// </summary>
    public CounterMetric(string name, IReadOnlyDictionary<string, string>? tags)
    {
        Name = name;
        Tags = tags;
    }

    /// <summary>
    /// 增加值
    /// </summary>
    public void Add(long value) => Interlocked.Add(ref _value, value);

    /// <summary>
    /// 转换为数据
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
/// 直方图指标
/// </summary>
public sealed class HistogramMetric
{
    private const int MaxValues = 10_000;
    private readonly double[] _values = new double[MaxValues];
    private readonly Lock _lock = new();
    private int _count;
    private int _writeIndex;

    /// <summary>
    /// 指标名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 标签
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; }

    /// <summary>
    /// 创建直方图
    /// </summary>
    public HistogramMetric(string name, IReadOnlyDictionary<string, string>? tags)
    {
        Name = name;
        Tags = tags;
    }

    /// <summary>
    /// 记录值
    /// </summary>
    public void Record(double value)
    {
        lock (_lock)
        {
            _values[_writeIndex] = value;
            _writeIndex = (_writeIndex + 1) % MaxValues;
            if (_count < MaxValues)
            {
                _count++;
            }
        }
    }

    /// <summary>
    /// 转换为数据
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
/// 仪表指标
/// </summary>
public sealed class GaugeMetric
{
    private double _value;

    /// <summary>
    /// 指标名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 标签
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; }

    /// <summary>
    /// 当前值
    /// </summary>
    public double Value => _value;

    /// <summary>
    /// 创建仪表
    /// </summary>
    public GaugeMetric(string name, IReadOnlyDictionary<string, string>? tags)
    {
        Name = name;
        Tags = tags;
    }

    /// <summary>
    /// 设置值
    /// </summary>
    public void Set(double value) => Interlocked.Exchange(ref _value, value);

    /// <summary>
    /// 转换为数据
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
