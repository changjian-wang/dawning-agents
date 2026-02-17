using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Core.Agent;
using FluentAssertions;

namespace Dawning.Agents.Tests.Agent;

public class CostTrackerTests
{
    [Fact]
    public void New_CostTracker_HasZeroCost()
    {
        var tracker = new CostTracker();

        tracker.TotalCost.Should().Be(0);
        tracker.Budget.Should().BeNull();
    }

    [Fact]
    public void New_CostTracker_WithBudget_HasCorrectBudget()
    {
        var tracker = new CostTracker(budget: 1.50m);

        tracker.Budget.Should().Be(1.50m);
    }

    [Fact]
    public void Add_AccumulatesCost()
    {
        var tracker = new CostTracker();

        tracker.Add(0.10m);
        tracker.Add(0.25m);

        tracker.TotalCost.Should().Be(0.35m);
    }

    [Fact]
    public void Add_NegativeCost_Throws()
    {
        var tracker = new CostTracker();

        var act = () => tracker.Add(-0.01m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Add_WithinBudget_DoesNotThrow()
    {
        var tracker = new CostTracker(budget: 1.00m);

        var act = () => tracker.Add(0.50m);

        act.Should().NotThrow();
    }

    [Fact]
    public void Add_ExceedsBudget_ThrowsBudgetExceededException()
    {
        var tracker = new CostTracker(budget: 0.10m);
        tracker.Add(0.08m);

        var act = () => tracker.Add(0.05m);

        act.Should()
            .Throw<BudgetExceededException>()
            .Where(ex => ex.TotalCost == 0.13m && ex.Budget == 0.10m);
    }

    [Fact]
    public void Add_NoBudget_NeverThrowsBudgetExceeded()
    {
        var tracker = new CostTracker();

        var act = () => tracker.Add(999999m);

        act.Should().NotThrow<BudgetExceededException>();
    }

    [Fact]
    public void Reset_ClearsCost()
    {
        var tracker = new CostTracker();
        tracker.Add(0.50m);

        tracker.Reset();

        tracker.TotalCost.Should().Be(0);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var tracker = new CostTracker(budget: 0.10m);
        tracker.Add(0.08m);

        tracker.Reset();
        var act = () => tracker.Add(0.05m);

        act.Should().NotThrow();
        tracker.TotalCost.Should().Be(0.05m);
    }
}

public class BudgetExceededExceptionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var ex = new BudgetExceededException(1.50m, 1.00m);

        ex.TotalCost.Should().Be(1.50m);
        ex.Budget.Should().Be(1.00m);
        ex.Message.Should().Contain("1.5000").And.Contain("1.0000");
    }
}

public class AgentOptionsMaxCostTests
{
    [Fact]
    public void Default_MaxCostPerRun_IsNull()
    {
        var options = new AgentOptions();
        options.MaxCostPerRun.Should().BeNull();
    }

    [Fact]
    public void Validate_NullMaxCostPerRun_DoesNotThrow()
    {
        var options = new AgentOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_PositiveMaxCostPerRun_DoesNotThrow()
    {
        var options = new AgentOptions { MaxCostPerRun = 1.00m };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ZeroMaxCostPerRun_Throws()
    {
        var options = new AgentOptions { MaxCostPerRun = 0m };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxCostPerRun*");
    }

    [Fact]
    public void Validate_NegativeMaxCostPerRun_Throws()
    {
        var options = new AgentOptions { MaxCostPerRun = -1m };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxCostPerRun*");
    }
}

public class AgentStepCostTests
{
    [Fact]
    public void AgentStep_DefaultCost_IsZero()
    {
        var step = new AgentStep { StepNumber = 1 };
        step.Cost.Should().Be(0);
    }

    [Fact]
    public void AgentStep_Cost_CanBeSet()
    {
        var step = new AgentStep { StepNumber = 1, Cost = 0.0025m };
        step.Cost.Should().Be(0.0025m);
    }
}

public class AgentResponseCostTests
{
    [Fact]
    public void Successful_TotalCost_SumsStepCosts()
    {
        var steps = new List<AgentStep>
        {
            new() { StepNumber = 1, Cost = 0.001m },
            new() { StepNumber = 2, Cost = 0.002m },
            new() { StepNumber = 3, Cost = 0.003m },
        };

        var response = AgentResponse.Successful("answer", steps, TimeSpan.FromSeconds(1));

        response.TotalCost.Should().Be(0.006m);
    }

    [Fact]
    public void Failed_TotalCost_SumsStepCosts()
    {
        var steps = new List<AgentStep>
        {
            new() { StepNumber = 1, Cost = 0.005m },
        };

        var response = AgentResponse.Failed("error", steps, TimeSpan.FromSeconds(1));

        response.TotalCost.Should().Be(0.005m);
    }

    [Fact]
    public void NoSteps_TotalCost_IsZero()
    {
        var response = AgentResponse.Successful("answer", [], TimeSpan.FromSeconds(1));
        response.TotalCost.Should().Be(0);
    }
}
