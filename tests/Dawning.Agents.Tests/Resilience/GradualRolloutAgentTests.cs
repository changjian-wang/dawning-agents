using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Resilience;
using Dawning.Agents.Core.Resilience;
using FluentAssertions;
using Moq;

namespace Dawning.Agents.Tests.Resilience;

/// <summary>
/// GradualRolloutAgent tests.
/// </summary>
public sealed class GradualRolloutAgentTests
{
    private readonly Mock<IAgent> _stableAgent = new();
    private readonly Mock<IAgent> _canaryAgent = new();
    private readonly Mock<IFeatureFlag> _featureFlag = new();

    private GradualRolloutAgent CreateAgent(GradualRolloutOptions? options = null)
    {
        _stableAgent.Setup(a => a.Name).Returns("stable");
        _stableAgent.Setup(a => a.Instructions).Returns("stable instructions");
        return new GradualRolloutAgent(
            _stableAgent.Object,
            _canaryAgent.Object,
            _featureFlag.Object,
            "canary-feature",
            options
        );
    }

    private static AgentResponse SuccessResponse(string answer) =>
        new() { Success = true, FinalAnswer = answer };

    [Fact]
    public async Task RunAsync_WhenCanaryDisabled_UsesStableAgent()
    {
        _featureFlag
            .Setup(f =>
                f.IsEnabledAsync(
                    "canary-feature",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        _stableAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResponse("stable"));

        var agent = CreateAgent();
        var result = await agent.RunAsync("test input");

        result.FinalAnswer.Should().Be("stable");
        _canaryAgent.Verify(
            a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RunAsync_WhenCanaryEnabled_UsesCanaryAgent()
    {
        _featureFlag
            .Setup(f =>
                f.IsEnabledAsync(
                    "canary-feature",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        _canaryAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResponse("canary"));

        var agent = CreateAgent();
        var result = await agent.RunAsync("test input");

        result.FinalAnswer.Should().Be("canary");
    }

    [Fact]
    public async Task RunAsync_WhenCanaryFails_FallsBackToStable()
    {
        _featureFlag
            .Setup(f =>
                f.IsEnabledAsync(
                    "canary-feature",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        _canaryAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("canary error"));
        _stableAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResponse("stable fallback"));

        var agent = CreateAgent();
        var result = await agent.RunAsync("test input");

        result.FinalAnswer.Should().Be("stable fallback");
    }

    [Fact]
    public async Task RunAsync_AutoRollback_WhenSuccessRateBelowThreshold()
    {
        var options = new GradualRolloutOptions
        {
            RollbackThreshold = 0.5f,
            MinSamplesBeforeRollback = 3,
        };

        _featureFlag
            .Setup(f =>
                f.IsEnabledAsync(
                    "canary-feature",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        _canaryAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail"));
        _stableAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResponse("stable"));

        var agent = CreateAgent(options);

        // Each call fails on canary, falls back to stable
        for (var i = 0; i < 3; i++)
        {
            await agent.RunAsync("input");
        }

        // After 3 failures (0% success rate < 50% threshold), rollback is triggered
        // The 4th call should go directly to stable, not even checking the feature flag
        await agent.RunAsync("input");

        // 3 canary attempts + no more canary calls after rollback
        _canaryAgent.Verify(
            a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );
    }

    [Fact]
    public async Task RunAsync_NoRollback_WhenBelowMinSamples()
    {
        var options = new GradualRolloutOptions
        {
            RollbackThreshold = 0.5f,
            MinSamplesBeforeRollback = 10,
        };

        _featureFlag
            .Setup(f =>
                f.IsEnabledAsync(
                    "canary-feature",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        _canaryAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail"));
        _stableAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResponse("stable"));

        var agent = CreateAgent(options);

        // 3 failures < minSamples of 10, so no rollback
        for (var i = 0; i < 3; i++)
        {
            await agent.RunAsync("input");
        }

        // 4th call should still try canary (not rolled back)
        await agent.RunAsync("input");

        // All 4 calls tried canary
        _canaryAgent.Verify(
            a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4)
        );
    }

    [Fact]
    public void Name_ReturnsStableAgentName()
    {
        var agent = CreateAgent();
        agent.Name.Should().Be("stable");
    }

    [Fact]
    public void Constructor_NullStableAgent_ShouldThrow()
    {
        var act = () =>
            new GradualRolloutAgent(null!, _canaryAgent.Object, _featureFlag.Object, "feature");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_EmptyFeatureName_ShouldThrow()
    {
        _stableAgent.Setup(a => a.Name).Returns("stable");
        var act = () =>
            new GradualRolloutAgent(
                _stableAgent.Object,
                _canaryAgent.Object,
                _featureFlag.Object,
                ""
            );
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GradualRolloutOptions_InvalidThreshold_ShouldThrow()
    {
        var options = new GradualRolloutOptions();
        var act = () => options.RollbackThreshold = -0.1f;
        act.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => options.RollbackThreshold = 1.1f;
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GradualRolloutOptions_InvalidMinSamples_ShouldThrow()
    {
        var options = new GradualRolloutOptions();
        var act = () => options.MinSamplesBeforeRollback = 0;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task RunAsync_WhenFeatureFlagThrows_FallsBackToStable()
    {
        _featureFlag
            .Setup(f =>
                f.IsEnabledAsync(
                    "canary-feature",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("flag service down"));
        _stableAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResponse("stable fallback"));

        var agent = CreateAgent();
        var result = await agent.RunAsync("test input");

        result.FinalAnswer.Should().Be("stable fallback");
        _canaryAgent.Verify(
            a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
