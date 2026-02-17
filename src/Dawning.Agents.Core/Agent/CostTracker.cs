using Dawning.Agents.Abstractions.Agent;

namespace Dawning.Agents.Core.Agent;

/// <summary>
/// 成本追踪器实现（线程安全）
/// </summary>
public sealed class CostTracker : ICostTracker
{
    private decimal _totalCost;
    private readonly object _lock = new();

    /// <inheritdoc />
    public decimal TotalCost
    {
        get
        {
            lock (_lock)
            {
                return _totalCost;
            }
        }
    }

    /// <inheritdoc />
    public decimal? Budget { get; }

    /// <summary>
    /// 创建成本追踪器
    /// </summary>
    /// <param name="budget">成本预算上限（USD），null 表示无限制</param>
    public CostTracker(decimal? budget = null)
    {
        Budget = budget;
    }

    /// <inheritdoc />
    public void Add(decimal cost)
    {
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost cannot be negative");
        }

        lock (_lock)
        {
            _totalCost += cost;

            if (Budget.HasValue && _totalCost > Budget.Value)
            {
                throw new BudgetExceededException(_totalCost, Budget.Value);
            }
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            _totalCost = 0;
        }
    }
}
