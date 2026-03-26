namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Cost tracker for a single agent run.
/// </summary>
/// <remarks>
/// Each agent run should use a separate <see cref="ICostTracker"/> instance.
/// Throws <see cref="BudgetExceededException"/> when MaxCostPerRun is configured and cost exceeds the budget.
/// </remarks>
public interface ICostTracker
{
    /// <summary>
    /// Accumulated total cost (USD).
    /// </summary>
    decimal TotalCost { get; }

    /// <summary>
    /// Maximum cost budget per run (USD). <c>null</c> means no limit.
    /// </summary>
    decimal? Budget { get; }

    /// <summary>
    /// Adds cost. Throws <see cref="BudgetExceededException"/> when accumulated cost exceeds the budget.
    /// </summary>
    /// <param name="cost">Cost to add (USD).</param>
    /// <exception cref="BudgetExceededException">Accumulated cost exceeds the budget.</exception>
    void Add(decimal cost);

    /// <summary>
    /// Resets cost to zero.
    /// </summary>
    void Reset();
}

/// <summary>
/// Exception thrown when cost exceeds the budget.
/// </summary>
public class BudgetExceededException : InvalidOperationException
{
    /// <summary>
    /// Accumulated cost (USD).
    /// </summary>
    public decimal TotalCost { get; }

    /// <summary>
    /// Budget limit (USD).
    /// </summary>
    public decimal Budget { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetExceededException"/> class.
    /// </summary>
    /// <param name="totalCost">Accumulated cost.</param>
    /// <param name="budget">Budget limit.</param>
    public BudgetExceededException(decimal totalCost, decimal budget)
        : base($"Cost budget exceeded: ${totalCost:F4} > ${budget:F4}")
    {
        TotalCost = totalCost;
        Budget = budget;
    }
}
