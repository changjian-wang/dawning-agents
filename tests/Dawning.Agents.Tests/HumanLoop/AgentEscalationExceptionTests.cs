using Dawning.Agents.Core.HumanLoop;
using FluentAssertions;

namespace Dawning.Agents.Tests.HumanLoop;

public class AgentEscalationExceptionTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var context = new Dictionary<string, object> { ["key"] = "value" };
        var solutions = new[] { "Solution 1", "Solution 2" };

        // Act
        var exception = new AgentEscalationException(
            "Test reason",
            "Test description",
            context,
            solutions
        );

        // Assert
        exception.Message.Should().Be("Test reason");
        exception.Reason.Should().Be("Test reason");
        exception.Description.Should().Be("Test description");
        exception.Context.Should().ContainKey("key");
        exception.Context["key"].Should().Be("value");
        exception.AttemptedSolutions.Should().HaveCount(2);
        exception.AttemptedSolutions.Should().Contain("Solution 1");
    }

    [Fact]
    public void Constructor_WithNullContext_ShouldUseEmptyDictionary()
    {
        // Act
        var exception = new AgentEscalationException("Reason", "Description");

        // Assert
        exception.Context.Should().NotBeNull();
        exception.Context.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullSolutions_ShouldUseEmptyList()
    {
        // Act
        var exception = new AgentEscalationException("Reason", "Description");

        // Assert
        exception.AttemptedSolutions.Should().NotBeNull();
        exception.AttemptedSolutions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithInnerException_ShouldSetInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new AgentEscalationException("Reason", "Description", innerException);

        // Assert
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void Constructor_WithInnerException_ShouldSetAllProperties()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        var context = new Dictionary<string, object> { ["error"] = "details" };
        var solutions = new[] { "Tried A", "Tried B" };

        // Act
        var exception = new AgentEscalationException(
            "Reason",
            "Description",
            innerException,
            context,
            solutions
        );

        // Assert
        exception.InnerException.Should().BeSameAs(innerException);
        exception.Context.Should().ContainKey("error");
        exception.AttemptedSolutions.Should().HaveCount(2);
    }
}
