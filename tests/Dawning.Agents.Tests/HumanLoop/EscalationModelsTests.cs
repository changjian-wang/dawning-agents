using Dawning.Agents.Abstractions.HumanLoop;
using FluentAssertions;

namespace Dawning.Agents.Tests.HumanLoop;

public class EscalationRequestTests
{
    [Fact]
    public void EscalationRequest_ShouldHaveDefaultValues()
    {
        // Act
        var request = new EscalationRequest
        {
            Reason = "Test reason",
            Description = "Test description",
        };

        // Assert
        request.Id.Should().NotBeNullOrEmpty();
        request.Severity.Should().Be(EscalationSeverity.Medium);
        request.AgentName.Should().BeNull();
        request.TaskId.Should().BeNull();
        request.Context.Should().BeEmpty();
        request.AttemptedSolutions.Should().BeEmpty();
        request.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EscalationRequest_ShouldAllowCustomValues()
    {
        // Arrange
        var context = new Dictionary<string, object> { ["key"] = "value" };
        var solutions = new[] { "Solution 1", "Solution 2" };

        // Act
        var request = new EscalationRequest
        {
            Id = "custom-id",
            Reason = "Task failed",
            Description = "The task failed after multiple attempts",
            Severity = EscalationSeverity.Critical,
            AgentName = "TestAgent",
            TaskId = "task-123",
            Context = context,
            AttemptedSolutions = solutions,
        };

        // Assert
        request.Id.Should().Be("custom-id");
        request.Severity.Should().Be(EscalationSeverity.Critical);
        request.AgentName.Should().Be("TestAgent");
        request.TaskId.Should().Be("task-123");
        request.Context.Should().ContainKey("key");
        request.AttemptedSolutions.Should().HaveCount(2);
    }
}

public class EscalationResultTests
{
    [Fact]
    public void EscalationResult_ShouldHaveRequiredProperties()
    {
        // Act
        var result = new EscalationResult { RequestId = "req-1" };

        // Assert
        result.RequestId.Should().Be("req-1");
        result.Action.Should().Be(EscalationAction.Resolved); // Default
        result.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EscalationResult_ShouldAllowAllProperties()
    {
        // Act
        var result = new EscalationResult
        {
            RequestId = "req-1",
            Action = EscalationAction.Resolved,
            Resolution = "Fixed the issue manually",
            Instructions = "Continue with next step",
            ResolvedBy = "admin",
        };

        // Assert
        result.Resolution.Should().Be("Fixed the issue manually");
        result.Instructions.Should().Be("Continue with next step");
        result.ResolvedBy.Should().Be("admin");
    }
}

public class EscalationSeverityTests
{
    [Theory]
    [InlineData(EscalationSeverity.Low, 0)]
    [InlineData(EscalationSeverity.Medium, 1)]
    [InlineData(EscalationSeverity.High, 2)]
    [InlineData(EscalationSeverity.Critical, 3)]
    public void EscalationSeverity_ShouldHaveCorrectValues(
        EscalationSeverity severity,
        int expected
    )
    {
        // Assert
        ((int)severity)
            .Should()
            .Be(expected);
    }
}

public class EscalationActionTests
{
    [Fact]
    public void EscalationAction_ShouldHaveAllExpectedValues()
    {
        // Assert
        Enum.GetValues<EscalationAction>().Should().HaveCount(5);
        Enum.GetValues<EscalationAction>()
            .Should()
            .Contain([
                EscalationAction.Resolved,
                EscalationAction.Skipped,
                EscalationAction.Aborted,
                EscalationAction.Delegated,
                EscalationAction.Retried,
            ]);
    }
}
