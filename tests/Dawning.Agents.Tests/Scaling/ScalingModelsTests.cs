namespace Dawning.Agents.Tests.Scaling;

using Dawning.Agents.Abstractions.Scaling;
using FluentAssertions;

public class ScalingModelsTests
{
    [Fact]
    public void ScalingOptions_DefaultValues_AreCorrect()
    {
        var options = new ScalingOptions();

        options.MinInstances.Should().Be(1);
        options.MaxInstances.Should().Be(10);
        options.TargetCpuPercent.Should().Be(70);
        options.TargetMemoryPercent.Should().Be(80);
        options.ScaleUpCooldownSeconds.Should().Be(60);
        options.ScaleDownCooldownSeconds.Should().Be(300);
        options.QueueCapacity.Should().Be(1000);
        options.WorkerCount.Should().Be(0);
    }

    [Fact]
    public void ScalingOptions_Validate_SucceedsWithValidConfig()
    {
        var options = new ScalingOptions
        {
            MinInstances = 2,
            MaxInstances = 20,
            TargetCpuPercent = 60,
            TargetMemoryPercent = 70,
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void ScalingOptions_Validate_ThrowsWhenMinInstancesLessThanOne()
    {
        var options = new ScalingOptions { MinInstances = 0 };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("MinInstances must be at least 1");
    }

    [Fact]
    public void ScalingOptions_Validate_ThrowsWhenMaxLessThanMin()
    {
        var options = new ScalingOptions { MinInstances = 5, MaxInstances = 3 };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("MaxInstances must be >= MinInstances");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void ScalingOptions_Validate_ThrowsOnInvalidCpuPercent(int cpuPercent)
    {
        var options = new ScalingOptions { TargetCpuPercent = cpuPercent };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("TargetCpuPercent must be between 1 and 100");
    }

    [Fact]
    public void ScalingOptions_GetActualWorkerCount_ReturnsConfiguredValue()
    {
        var options = new ScalingOptions { WorkerCount = 8 };

        options.GetActualWorkerCount().Should().Be(8);
    }

    [Fact]
    public void ScalingOptions_GetActualWorkerCount_ReturnsAutoDetectedValue()
    {
        var options = new ScalingOptions { WorkerCount = 0 };

        options.GetActualWorkerCount().Should().Be(Environment.ProcessorCount * 2);
    }

    [Fact]
    public void ScalingMetrics_CanBeCreated()
    {
        var metrics = new ScalingMetrics
        {
            CpuPercent = 75.5,
            MemoryPercent = 60.0,
            QueueLength = 100,
            ActiveRequests = 50,
            AvgLatencyMs = 150.5,
        };

        metrics.CpuPercent.Should().Be(75.5);
        metrics.MemoryPercent.Should().Be(60.0);
        metrics.QueueLength.Should().Be(100);
        metrics.ActiveRequests.Should().Be(50);
        metrics.AvgLatencyMs.Should().Be(150.5);
        metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ScalingDecision_None_CreatesCorrectDecision()
    {
        var decision = ScalingDecision.None;

        decision.Action.Should().Be(ScalingAction.None);
        decision.Delta.Should().Be(0);
        decision.Reason.Should().BeNull();
    }

    [Fact]
    public void ScalingDecision_ScaleUp_CreatesCorrectDecision()
    {
        var decision = ScalingDecision.ScaleUp(2, "High CPU usage");

        decision.Action.Should().Be(ScalingAction.ScaleUp);
        decision.Delta.Should().Be(2);
        decision.Reason.Should().Be("High CPU usage");
    }

    [Fact]
    public void ScalingDecision_ScaleDown_CreatesCorrectDecision()
    {
        var decision = ScalingDecision.ScaleDown(1, "Low utilization");

        decision.Action.Should().Be(ScalingAction.ScaleDown);
        decision.Delta.Should().Be(1);
        decision.Reason.Should().Be("Low utilization");
    }

    [Fact]
    public void CircuitState_HasCorrectValues()
    {
        CircuitState.Closed.Should().Be((CircuitState)0);
        CircuitState.Open.Should().Be((CircuitState)1);
        CircuitState.HalfOpen.Should().Be((CircuitState)2);
    }
}
