namespace Dawning.Agents.Core.Observability;

using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Observability;

/// <summary>
/// 用于开发/测试的内存指标收集器
/// </summary>
public class MetricsCollector
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
        IDictionary<string, string>? tags = null
    )
    {
        var key = GetMetricKey(name, tags);
        var counter = _counters.GetOrAdd(key, _ => new CounterMetric(name, tags));
        counter.Add(value);
    }

    /// <summary>
    /// 记录直方图值
    /// </summary>
    public void RecordHistogram(string name, double value, IDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        var histogram = _histograms.GetOrAdd(key, _ => new HistogramMetric(name, tags));
        histogram.Record(value);
    }

    /// <summary>
    /// 设置仪表值
    /// </summary>
    public void SetGauge(string name, double value, IDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        var gauge = _gauges.GetOrAdd(key, _ => new GaugeMetric(name, tags));
        gauge.Set(value);
    }

    /// <summary>
    /// 获取计数器值
    /// </summary>
    public long? GetCounter(string name, IDictionary<string, string>? tags = null)
    {
        var key = GetMetricKey(name, tags);
        return _counters.TryGetValue(key, out var counter) ? counter.Value : null;
    }

    /// <summary>
    /// 获取仪表值
    /// </summary>
    public double? GetGauge(string name, IDictionary<string, string>? tags = null)
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
            Timestamp = DateTime.UtcNow,
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

    private static string GetMetricKey(string name, IDictionary<string, string>? tags)
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
public class CounterMetric
{
    private long _value;

    /// <summary>
    /// 指标名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 标签
    /// </summary>
    public IDictionary<string, string>? Tags { get; }

    /// <summary>
    /// 当前值
    /// </summary>
    public long Value => _value;

    /// <summary>
    /// 创建计数器
    /// </summary>
    public CounterMetric(string name, IDictionary<string, string>? tags)
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
        new() { Name = Name, Type = "counter", Value = _value, Tags = Tags };
}

/// <summary>
/// 直方图指标
/// </summary>
public class HistogramMetric
{
    private readonly List<double> _values = [];
    private readonly object _lock = new();

    /// <summary>
    /// 指标名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 标签
    /// </summary>
    public IDictionary<string, string>? Tags { get; }

    /// <summary>
    /// 创建直方图
    /// </summary>
    public HistogramMetric(string name, IDictionary<string, string>? tags)
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
            _values.Add(value);
        }
    }

    /// <summary>
    /// 转换为数据
    /// </summary>
    public MetricData ToData()
    {
        lock (_lock)
        {
            var sorted = _values.OrderBy(v => v).ToList();
            return new MetricData
            {
                Name = Name,
                Type = "histogram",
                Count = sorted.Count,
                Sum = sorted.Sum(),
                Min = sorted.Count > 0 ? sorted[0] : 0,
                Max = sorted.Count > 0 ? sorted[^1] : 0,
                P50 = GetPercentile(sorted, 0.5),
                P95 = GetPercentile(sorted, 0.95),
                P99 = GetPercentile(sorted, 0.99),
                Tags = Tags,
            };
        }
    }

    private static double GetPercentile(List<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var index = (int)(percentile * (sorted.Count - 1));
        return sorted[index];
    }
}

/// <summary>
/// 仪表指标
/// </summary>
public class GaugeMetric
{
    private double _value;

    /// <summary>
    /// 指标名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 标签
    /// </summary>
    public IDictionary<string, string>? Tags { get; }

    /// <summary>
    /// 当前值
    /// </summary>
    public double Value => _value;

    /// <summary>
    /// 创建仪表
    /// </summary>
    public GaugeMetric(string name, IDictionary<string, string>? tags)
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
        new() { Name = Name, Type = "gauge", Value = _value, Tags = Tags };
}
