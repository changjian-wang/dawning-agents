namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// 单次 Agent 运行的成本追踪器
/// </summary>
/// <remarks>
/// 每个 Agent Run 应使用独立的 ICostTracker 实例。
/// 当配置了 MaxCostPerRun 且成本超出预算时抛出 <see cref="BudgetExceededException"/>。
/// </remarks>
public interface ICostTracker
{
    /// <summary>
    /// 累计总成本（USD）
    /// </summary>
    decimal TotalCost { get; }

    /// <summary>
    /// 最大单次运行成本预算（USD），null 表示无限制
    /// </summary>
    decimal? Budget { get; }

    /// <summary>
    /// 累加成本，超出预算时抛出 <see cref="BudgetExceededException"/>
    /// </summary>
    /// <param name="cost">本次成本（USD）</param>
    /// <exception cref="BudgetExceededException">累计成本超出预算</exception>
    void Add(decimal cost);

    /// <summary>
    /// 重置成本为零
    /// </summary>
    void Reset();
}

/// <summary>
/// 成本超出预算异常
/// </summary>
public class BudgetExceededException : InvalidOperationException
{
    /// <summary>
    /// 累计成本（USD）
    /// </summary>
    public decimal TotalCost { get; }

    /// <summary>
    /// 预算上限（USD）
    /// </summary>
    public decimal Budget { get; }

    /// <summary>
    /// 创建成本超出预算异常
    /// </summary>
    /// <param name="totalCost">累计成本</param>
    /// <param name="budget">预算上限</param>
    public BudgetExceededException(decimal totalCost, decimal budget)
        : base($"Cost budget exceeded: ${totalCost:F4} > ${budget:F4}")
    {
        TotalCost = totalCost;
        Budget = budget;
    }
}
