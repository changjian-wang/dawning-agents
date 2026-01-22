using Dawning.Agents.Abstractions.Safety;
using FluentAssertions;

namespace Dawning.Agents.Tests.Safety;

public class GuardrailResultTests
{
    [Fact]
    public void Pass_ShouldCreatePassedResult()
    {
        // Act
        var result = GuardrailResult.Pass();

        // Assert
        result.Passed.Should().BeTrue();
        result.Message.Should().BeNull();
        result.TriggeredBy.Should().BeNull();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void PassWithContent_ShouldCreatePassedResultWithProcessedContent()
    {
        // Arrange
        var content = "processed content";

        // Act
        var result = GuardrailResult.PassWithContent(content);

        // Assert
        result.Passed.Should().BeTrue();
        result.ProcessedContent.Should().Be(content);
    }

    [Fact]
    public void Fail_ShouldCreateFailedResult()
    {
        // Arrange
        var message = "Test failure";
        var triggeredBy = "TestGuardrail";

        // Act
        var result = GuardrailResult.Fail(message, triggeredBy);

        // Assert
        result.Passed.Should().BeFalse();
        result.Message.Should().Be(message);
        result.TriggeredBy.Should().Be(triggeredBy);
    }

    [Fact]
    public void Fail_WithIssues_ShouldContainIssues()
    {
        // Arrange
        var issues = new List<GuardrailIssue>
        {
            new()
            {
                Type = "TestIssue",
                Description = "Test description",
                Severity = IssueSeverity.Error,
            },
        };

        // Act
        var result = GuardrailResult.Fail("Test", "TestGuardrail", issues);

        // Assert
        result.Issues.Should().HaveCount(1);
        result.Issues[0].Type.Should().Be("TestIssue");
        result.Issues[0].Severity.Should().Be(IssueSeverity.Error);
    }
}

public class GuardrailIssueTests
{
    [Fact]
    public void GuardrailIssue_ShouldHaveDefaultSeverity()
    {
        // Act
        var issue = new GuardrailIssue { Type = "Test", Description = "Test" };

        // Assert
        issue.Severity.Should().Be(IssueSeverity.Warning);
    }

    [Theory]
    [InlineData(IssueSeverity.Info)]
    [InlineData(IssueSeverity.Warning)]
    [InlineData(IssueSeverity.Error)]
    [InlineData(IssueSeverity.Critical)]
    public void GuardrailIssue_ShouldSupportAllSeverityLevels(IssueSeverity severity)
    {
        // Act
        var issue = new GuardrailIssue
        {
            Type = "Test",
            Description = "Test",
            Severity = severity,
        };

        // Assert
        issue.Severity.Should().Be(severity);
    }

    [Fact]
    public void GuardrailIssue_ShouldStorePositionAndLength()
    {
        // Act
        var issue = new GuardrailIssue
        {
            Type = "Test",
            Description = "Test",
            Position = 10,
            Length = 5,
            MatchedContent = "12345",
        };

        // Assert
        issue.Position.Should().Be(10);
        issue.Length.Should().Be(5);
        issue.MatchedContent.Should().Be("12345");
    }
}
