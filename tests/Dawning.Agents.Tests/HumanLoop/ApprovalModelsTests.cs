using Dawning.Agents.Abstractions.HumanLoop;
using FluentAssertions;

namespace Dawning.Agents.Tests.HumanLoop;

public class ApprovalResultTests
{
    [Fact]
    public void AutoApproved_ShouldCreateCorrectResult()
    {
        // Act
        var result = ApprovalResult.AutoApproved("test-action");

        // Assert
        result.Action.Should().Be("test-action");
        result.IsApproved.Should().BeTrue();
        result.IsAutoApproved.Should().BeTrue();
        result.IsTimedOut.Should().BeFalse();
        result.ApprovedBy.Should().BeNull();
        result.RejectionReason.Should().BeNull();
        result.ModifiedAction.Should().BeNull();
    }

    [Fact]
    public void Approved_ShouldCreateCorrectResult()
    {
        // Act
        var result = ApprovalResult.Approved("test-action", "admin");

        // Assert
        result.Action.Should().Be("test-action");
        result.IsApproved.Should().BeTrue();
        result.IsAutoApproved.Should().BeFalse();
        result.ApprovedBy.Should().Be("admin");
    }

    [Fact]
    public void Approved_WithoutApprover_ShouldCreateCorrectResult()
    {
        // Act
        var result = ApprovalResult.Approved("test-action");

        // Assert
        result.IsApproved.Should().BeTrue();
        result.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public void Rejected_ShouldCreateCorrectResult()
    {
        // Act
        var result = ApprovalResult.Rejected("test-action", "Not allowed", "admin");

        // Assert
        result.Action.Should().Be("test-action");
        result.IsApproved.Should().BeFalse();
        result.IsTimedOut.Should().BeFalse();
        result.RejectionReason.Should().Be("Not allowed");
        result.ApprovedBy.Should().Be("admin");
    }

    [Fact]
    public void Rejected_WithMinimalParams_ShouldCreateCorrectResult()
    {
        // Act
        var result = ApprovalResult.Rejected("test-action");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.RejectionReason.Should().BeNull();
        result.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public void Modified_ShouldCreateCorrectResult()
    {
        // Act
        var result = ApprovalResult.Modified("original-action", "modified-action", "admin");

        // Assert
        result.Action.Should().Be("original-action");
        result.IsApproved.Should().BeTrue();
        result.ModifiedAction.Should().Be("modified-action");
        result.ApprovedBy.Should().Be("admin");
    }

    [Fact]
    public void TimedOut_ShouldCreateCorrectResult()
    {
        // Act
        var result = ApprovalResult.TimedOut("test-action");

        // Assert
        result.Action.Should().Be("test-action");
        result.IsApproved.Should().BeFalse();
        result.IsTimedOut.Should().BeTrue();
        result.RejectionReason.Should().Be("审批请求超时");
    }
}

public class ApprovalConfigTests
{
    [Fact]
    public void ApprovalConfig_ShouldHaveDefaultValues()
    {
        // Act
        var config = new ApprovalConfig();

        // Assert
        config.RequireApprovalForLowRisk.Should().BeFalse();
        config.RequireApprovalForMediumRisk.Should().BeTrue();
        config.ApprovalTimeout.Should().Be(TimeSpan.FromMinutes(30));
        config.DefaultOnTimeout.Should().Be("reject");
    }

    [Fact]
    public void ApprovalConfig_ShouldAllowCustomValues()
    {
        // Act
        var config = new ApprovalConfig
        {
            RequireApprovalForLowRisk = true,
            RequireApprovalForMediumRisk = false,
            ApprovalTimeout = TimeSpan.FromMinutes(5),
            DefaultOnTimeout = "approve",
        };

        // Assert
        config.RequireApprovalForLowRisk.Should().BeTrue();
        config.RequireApprovalForMediumRisk.Should().BeFalse();
        config.ApprovalTimeout.Should().Be(TimeSpan.FromMinutes(5));
        config.DefaultOnTimeout.Should().Be("approve");
    }
}

public class HumanLoopOptionsTests
{
    [Fact]
    public void HumanLoopOptions_ShouldHaveCorrectSectionName()
    {
        // Assert
        HumanLoopOptions.SectionName.Should().Be("HumanLoop");
    }

    [Fact]
    public void HumanLoopOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new HumanLoopOptions();

        // Assert
        options.ConfirmBeforeExecution.Should().BeFalse();
        options.ReviewBeforeReturn.Should().BeFalse();
        options.RequireApprovalForMediumRisk.Should().BeTrue();
        options.DefaultTimeout.Should().Be(TimeSpan.FromMinutes(30));
        options.MaxRetries.Should().Be(3);
        options.HighRiskKeywords.Should().NotBeEmpty();
        options.CriticalRiskKeywords.Should().NotBeEmpty();
    }

    [Fact]
    public void HumanLoopOptions_HighRiskKeywords_ShouldContainExpectedValues()
    {
        // Act
        var options = new HumanLoopOptions();

        // Assert
        options.HighRiskKeywords.Should().Contain("delete");
        options.HighRiskKeywords.Should().Contain("删除");
        options.HighRiskKeywords.Should().Contain("transfer");
        options.HighRiskKeywords.Should().Contain("转账");
    }

    [Fact]
    public void HumanLoopOptions_CriticalRiskKeywords_ShouldContainExpectedValues()
    {
        // Act
        var options = new HumanLoopOptions();

        // Assert
        options.CriticalRiskKeywords.Should().Contain("production");
        options.CriticalRiskKeywords.Should().Contain("生产");
        options.CriticalRiskKeywords.Should().Contain("credentials");
        options.CriticalRiskKeywords.Should().Contain("凭证");
    }
}
