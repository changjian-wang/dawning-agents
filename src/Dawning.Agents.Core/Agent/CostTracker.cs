using Dawning.Agents.Abstractions.Agent;

namespace Dawning.Agents.Core.Agent;

/// <summary>
/// Cost tracker implementation (thread-safe).
/// </summary>
public sealed class CostTracker : ICostTracker
{
    private decimal _totalCost;
    private readonly Lock _lock = new();

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
    /// Initializes a new instance of the <see cref="CostTracker"/> class.
    /// </summary>
    /// <param name="budget">Cost budget limit (USD), or <see langword="null"/> for unlimited.</param>
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
