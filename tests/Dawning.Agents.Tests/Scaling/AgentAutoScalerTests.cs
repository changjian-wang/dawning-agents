namespace Dawning.Agents.Tests.Scaling;

using Dawning.Agents.Abstractions.Scaling;
using Dawning.Agents.Core.Scaling;
using FluentAssertions;

public class AgentAutoScalerTests
{
    [Fact]
    public void Constructor_SetsInitialInstanceCount()
    {
        var options = new ScalingOptions { MinInstances = 3 };
        var scaler = CreateScaler(options);

        scaler.CurrentInstances.Should().Be(3);
    }

    [Fact]
    public async Task EvaluateAsync_NoAction_WhenMetricsNormal()
    {
        var options = new ScalingOptions { TargetCpuPercent = 70, TargetMemoryPercent = 80 };
        var metrics = new ScalingMetrics
        {
            CpuPercent = 50,
            MemoryPercent = 50,
            QueueLength = 5,
        };

        var scaler = CreateScaler(options, metrics);
        var decision = await scaler.EvaluateAsync();

        decision.Action.Should().Be(ScalingAction.None);
    }

    [Fact]
    public async Task EvaluateAsync_ScalesUp_WhenCpuHigh()
    {
        var options = new ScalingOptions
        {
            MinInstances = 1,
            MaxInstances = 10,
            TargetCpuPercent = 70,
            ScaleUpCooldownSeconds = 0,
        };
        var metrics = new ScalingMetrics { CpuPercent = 90, MemoryPercent = 50, QueueLength = 5 };

        var scaledTo = 0;
        var scaler = CreateScaler(options, metrics, count => scaledTo = count);
        var decision = await scaler.EvaluateAsync();

        decision.Action.Should().Be(ScalingAction.ScaleUp);
        scaledTo.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task EvaluateAsync_ScalesUp_WhenMemoryHigh()
    {
        var options = new ScalingOptions
        {
            MinInstances = 1,
            MaxInstances = 10,
            TargetMemoryPercent = 80,
            ScaleUpCooldownSeconds = 0,
        };
        var metrics = new ScalingMetrics { CpuPercent = 50, MemoryPercent = 95, QueueLength = 5 };

        var scaledTo = 0;
        var scaler = CreateScaler(options, metrics, count => scaledTo = count);
        var decision = await scaler.EvaluateAsync();

        decision.Action.Should().Be(ScalingAction.ScaleUp);
        scaledTo.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task EvaluateAsync_ScalesUp_WhenQueueLong()
    {
        var options = new ScalingOptions
        {
            MinInstances = 1,
            MaxInstances = 10,
            TargetCpuPercent = 70,
            ScaleUpCooldownSeconds = 0,
        };
        var metrics = new ScalingMetrics
        {
            CpuPercent = 50,
            MemoryPercent = 50,
            QueueLength = 100, // > 1 * 10
        };

        var scaler = CreateScaler(options, metrics);
        var decision = await scaler.EvaluateAsync();

        decision.Action.Should().Be(ScalingAction.ScaleUp);
    }

    [Fact]
    public async Task EvaluateAsync_ScalesDown_WhenUtilizationLow()
    {
        var options = new ScalingOptions
        {
            MinInstances = 1,
            MaxInstances = 10,
            TargetCpuPercent = 70,
            TargetMemoryPercent = 80,
            ScaleDownCooldownSeconds = 0,
        };
        var metrics = new ScalingMetrics
        {
            CpuPercent = 20, // < 70 * 0.5
            MemoryPercent = 20, // < 80 * 0.5
            QueueLength = 1,
        };

        var scaledTo = 0;
        var scaler = CreateScaler(options, metrics, count => scaledTo = count);
        scaler.SetCurrentInstances(5); // 需要有多于最小实例才能缩容

        var decision = await scaler.EvaluateAsync();

        decision.Action.Should().Be(ScalingAction.ScaleDown);
    }

    [Fact]
    public async Task EvaluateAsync_RespectsMaxInstances()
    {
        var options = new ScalingOptions
        {
            MinInstances = 1,
            MaxInstances = 3,
            TargetCpuPercent = 70,
            ScaleUpCooldownSeconds = 0,
        };
        var metrics = new ScalingMetrics { CpuPercent = 200, MemoryPercent = 50, QueueLength = 5 };

        var scaledTo = 0;
        var scaler = CreateScaler(options, metrics, count => scaledTo = count);
        scaler.SetCurrentInstances(2);

        await scaler.EvaluateAsync();

        scaledTo.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task EvaluateAsync_RespectsMinInstances()
    {
        var options = new ScalingOptions
        {
            MinInstances = 2,
            MaxInstances = 10,
            TargetCpuPercent = 70,
            TargetMemoryPercent = 80,
            ScaleDownCooldownSeconds = 0,
        };
        var metrics = new ScalingMetrics { CpuPercent = 10, MemoryPercent = 10, QueueLength = 1 };

        var scaledTo = 0;
        var scaler = CreateScaler(options, metrics, count => scaledTo = count);
        scaler.SetCurrentInstances(3);

        await scaler.EvaluateAsync();

        scaledTo.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task EvaluateAsync_RespectsScaleUpCooldown()
    {
        var options = new ScalingOptions
        {
            MinInstances = 1,
            MaxInstances = 10,
            TargetCpuPercent = 70,
            ScaleUpCooldownSeconds = 60, // 1 minute cooldown
        };
        var metrics = new ScalingMetrics { CpuPercent = 90, MemoryPercent = 50, QueueLength = 5 };

        var scaleCount = 0;
        var scaler = CreateScaler(options, metrics, _ => scaleCount++);

        // First scale up
        await scaler.EvaluateAsync();

        // Second attempt should be blocked by cooldown
        var decision = await scaler.EvaluateAsync();

        scaleCount.Should().Be(1);
    }

    [Fact]
    public void LastScaleUpTime_IsNullInitially()
    {
        var scaler = CreateScaler(new ScalingOptions());

        scaler.LastScaleUpTime.Should().BeNull();
    }

    [Fact]
    public async Task LastScaleUpTime_IsSetAfterScaleUp()
    {
        var options = new ScalingOptions
        {
            MinInstances = 1,
            MaxInstances = 10,
            TargetCpuPercent = 70,
            ScaleUpCooldownSeconds = 0,
        };
        var metrics = new ScalingMetrics { CpuPercent = 90, MemoryPercent = 50, QueueLength = 5 };

        var scaler = CreateScaler(options, metrics);
        await scaler.EvaluateAsync();

        scaler.LastScaleUpTime.Should().NotBeNull();
        scaler.LastScaleUpTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    private static AgentAutoScaler CreateScaler(
        ScalingOptions options,
        ScalingMetrics? metrics = null,
        Action<int>? onScale = null
    )
    {
        var defaultMetrics = metrics ?? new ScalingMetrics { CpuPercent = 50, MemoryPercent = 50, QueueLength = 5 };

        return new AgentAutoScaler(
            options,
            () => Task.FromResult(defaultMetrics),
            count =>
            {
                onScale?.Invoke(count);
                return Task.CompletedTask;
            }
        );
    }
}
