using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// DefaultSkillEvolutionPolicy 测试
/// </summary>
public sealed class DefaultSkillEvolutionPolicyTests
{
    private readonly DefaultSkillEvolutionPolicy _policy = new();

    #region EvaluatePromotionAsync

    [Fact]
    public async Task EvaluatePromotion_HighSuccess_ShouldPromoteToGlobal()
    {
        var stats = new ToolUsageStats
        {
            ToolName = "tool",
            TotalCalls = 10,
            SuccessCount = 9,
            FailureCount = 1,
        };

        var decision = await _policy.EvaluatePromotionAsync("tool", stats);

        decision.ShouldPromote.Should().BeTrue();
        decision.TargetScope.Should().Be(ToolScope.Global);
    }

    [Fact]
    public async Task EvaluatePromotion_GoodSuccess_ShouldPromoteToUser()
    {
        var stats = new ToolUsageStats
        {
            ToolName = "tool",
            TotalCalls = 5,
            SuccessCount = 4,
            FailureCount = 1,
        };

        var decision = await _policy.EvaluatePromotionAsync("tool", stats);

        decision.ShouldPromote.Should().BeTrue();
        decision.TargetScope.Should().Be(ToolScope.User);
    }

    [Fact]
    public async Task EvaluatePromotion_LowSuccess_ShouldNotPromote()
    {
        var stats = new ToolUsageStats
        {
            ToolName = "tool",
            TotalCalls = 5,
            SuccessCount = 2,
            FailureCount = 3,
        };

        var decision = await _policy.EvaluatePromotionAsync("tool", stats);

        decision.ShouldPromote.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluatePromotion_TooFewCalls_ShouldNotPromote()
    {
        var stats = new ToolUsageStats
        {
            ToolName = "tool",
            TotalCalls = 2,
            SuccessCount = 2,
            FailureCount = 0,
        };

        var decision = await _policy.EvaluatePromotionAsync("tool", stats);

        decision.ShouldPromote.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluatePromotion_NullToolName_ShouldThrow()
    {
        var stats = new ToolUsageStats { ToolName = "tool" };
        var act = () => _policy.EvaluatePromotionAsync(null!, stats);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EvaluatePromotion_NullStats_ShouldThrow()
    {
        var act = () => _policy.EvaluatePromotionAsync("tool", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluatePromotion_ExactThreshold_User()
    {
        // Exactly 80% and 3 calls → should promote to User
        var stats = new ToolUsageStats
        {
            ToolName = "tool",
            TotalCalls = 5,
            SuccessCount = 4,
            FailureCount = 1,
        };

        var decision = await _policy.EvaluatePromotionAsync("tool", stats);
        decision.ShouldPromote.Should().BeTrue();
        decision.TargetScope.Should().Be(ToolScope.User);
    }

    [Fact]
    public async Task EvaluatePromotion_ExactThreshold_Global()
    {
        // Exactly 90% and 10 calls → should promote to Global
        var stats = new ToolUsageStats
        {
            ToolName = "tool",
            TotalCalls = 10,
            SuccessCount = 9,
            FailureCount = 1,
        };

        var decision = await _policy.EvaluatePromotionAsync("tool", stats);
        decision.ShouldPromote.Should().BeTrue();
        decision.TargetScope.Should().Be(ToolScope.Global);
    }

    #endregion

    #region ShouldRetireAsync

    [Fact]
    public async Task ShouldRetire_VeryLowSuccess_ShouldRetire()
    {
        var stats = new ToolUsageStats
        {
            ToolName = "tool",
            TotalCalls = 5,
            SuccessCount = 0,
            FailureCount = 5,
        };

        var result = await _policy.ShouldRetireAsync("tool", stats);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldRetire_HighSuccess_ShouldNotRetire()
    {
        var stats = new ToolUsageStats
        {
            ToolName = "tool",
            TotalCalls = 10,
            SuccessCount = 9,
            FailureCount = 1,
        };

        var result = await _policy.ShouldRetireAsync("tool", stats);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldRetire_TooFewCalls_ShouldNotRetire()
    {
        var stats = new ToolUsageStats
        {
            ToolName = "tool",
            TotalCalls = 3,
            SuccessCount = 0,
            FailureCount = 3,
        };

        var result = await _policy.ShouldRetireAsync("tool", stats);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldRetire_NullToolName_ShouldThrow()
    {
        var stats = new ToolUsageStats { ToolName = "tool" };
        var act = () => _policy.ShouldRetireAsync(null!, stats);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ShouldRetire_NullStats_ShouldThrow()
    {
        var act = () => _policy.ShouldRetireAsync("tool", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region PromotionDecision Record

    [Fact]
    public void PromotionDecision_ShouldExposeProperties()
    {
        var decision = new PromotionDecision(true, ToolScope.Global, "high quality");
        decision.ShouldPromote.Should().BeTrue();
        decision.TargetScope.Should().Be(ToolScope.Global);
        decision.Reason.Should().Be("high quality");
    }

    #endregion
}
