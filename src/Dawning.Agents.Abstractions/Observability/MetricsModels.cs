namespace Dawning.Agents.Abstractions.Observability;

/// <summary>
/// 指标数据
/// </summary>
public record MetricData
{
    /// <summary>
    /// 指标名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 指标类型（counter, histogram, gauge）
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// 值（用于计数器和仪表）
    /// </summary>
    public double Value { get; init; }

    /// <summary>
    /// 计数（用于直方图）
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// 总和（用于直方图）
    /// </summary>
    public double Sum { get; init; }

    /// <summary>
    /// 最小值
    /// </summary>
    public double Min { get; init; }

    /// <summary>
    /// 最大值
    /// </summary>
    public double Max { get; init; }

    /// <summary>
    /// 50 百分位
    /// </summary>
    public double P50 { get; init; }

    /// <summary>
    /// 95 百分位
    /// </summary>
    public double P95 { get; init; }

    /// <summary>
    /// 99 百分位
    /// </summary>
    public double P99 { get; init; }

    /// <summary>
    /// 标签
    /// </summary>
    public IDictionary<string, string>? Tags { get; init; }
}

/// <summary>
/// 指标快照
/// </summary>
public record MetricsSnapshot
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// 计数器指标
    /// </summary>
    public IReadOnlyList<MetricData> Counters { get; init; } = [];

    /// <summary>
    /// 直方图指标
    /// </summary>
    public IReadOnlyList<MetricData> Histograms { get; init; } = [];

    /// <summary>
    /// 仪表指标
    /// </summary>
    public IReadOnlyList<MetricData> Gauges { get; init; } = [];
}
