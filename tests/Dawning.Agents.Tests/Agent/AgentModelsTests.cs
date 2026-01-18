using Dawning.Agents.Abstractions.Agent;
using FluentAssertions;

namespace Dawning.Agents.Tests.Agent;

public class AgentModelsTests
{
    [Fact]
    public void AgentContext_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var context = new AgentContext { UserInput = "test input" };

        // Assert
        context.SessionId.Should().NotBeNullOrEmpty();
        context.UserInput.Should().Be("test input");
        context.Steps.Should().BeEmpty();
        context.MaxSteps.Should().Be(10);
        context.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void AgentContext_ShouldAllowAddingSteps()
    {
        // Arrange
        var context = new AgentContext { UserInput = "test" };
        var step = new AgentStep
        {
            StepNumber = 1,
            Thought = "thinking...",
            Action = "Search",
            ActionInput = "query"
        };

        // Act
        context.Steps.Add(step);

        // Assert
        context.Steps.Should().HaveCount(1);
        context.Steps[0].Should().Be(step);
    }

    [Fact]
    public void AgentStep_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var step = new AgentStep { StepNumber = 1 };

        // Assert
        step.StepNumber.Should().Be(1);
        step.Thought.Should().BeNull();
        step.Action.Should().BeNull();
        step.ActionInput.Should().BeNull();
        step.Observation.Should().BeNull();
        step.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AgentResponse_Successful_ShouldCreateCorrectResponse()
    {
        // Arrange
        var steps = new List<AgentStep>
        {
            new() { StepNumber = 1, Thought = "thinking" }
        };
        var duration = TimeSpan.FromSeconds(1);

        // Act
        var response = AgentResponse.Successful("final answer", steps, duration);

        // Assert
        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("final answer");
        response.Error.Should().BeNull();
        response.Steps.Should().BeEquivalentTo(steps);
        response.Duration.Should().Be(duration);
    }

    [Fact]
    public void AgentResponse_Failed_ShouldCreateCorrectResponse()
    {
        // Arrange
        var steps = new List<AgentStep>();
        var duration = TimeSpan.FromSeconds(2);

        // Act
        var response = AgentResponse.Failed("error message", steps, duration);

        // Assert
        response.Success.Should().BeFalse();
        response.FinalAnswer.Should().BeNull();
        response.Error.Should().Be("error message");
        response.Steps.Should().BeEmpty();
        response.Duration.Should().Be(duration);
    }

    [Fact]
    public void AgentOptions_Validate_ShouldPassWithValidOptions()
    {
        // Arrange
        var options = new AgentOptions
        {
            Name = "TestAgent",
            Instructions = "Test instructions",
            MaxSteps = 5
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void AgentOptions_Validate_ShouldThrowWhenNameIsEmpty()
    {
        // Arrange
        var options = new AgentOptions { Name = "" };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Name*");
    }

    [Fact]
    public void AgentOptions_Validate_ShouldThrowWhenMaxStepsIsZero()
    {
        // Arrange
        var options = new AgentOptions { MaxSteps = 0 };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MaxSteps*");
    }
}
