using Dawning.Agents.Abstractions.Handoff;
using FluentAssertions;

namespace Dawning.Agents.Tests.Handoff;

public class HandoffOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeHandoff()
    {
        // Assert
        HandoffOptions.SectionName.Should().Be("Handoff");
    }

    [Fact]
    public void DefaultValues_ShouldBeReasonable()
    {
        // Act
        var options = new HandoffOptions();

        // Assert
        options.MaxHandoffDepth.Should().Be(5);
        options.TimeoutSeconds.Should().Be(60);
        options.TotalTimeoutSeconds.Should().Be(300);
        options.AllowCycles.Should().BeFalse();
        options.FallbackToSource.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldPass_WithDefaultValues()
    {
        // Arrange
        var options = new HandoffOptions();

        // Act
        var action = () => options.Validate();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Validate_ShouldThrow_WhenMaxHandoffDepthIsZero()
    {
        // Arrange
        var options = new HandoffOptions { MaxHandoffDepth = 0 };

        // Act
        var action = () => options.Validate();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*MaxHandoffDepth*");
    }

    [Fact]
    public void Validate_ShouldThrow_WhenTimeoutIsZero()
    {
        // Arrange
        var options = new HandoffOptions { TimeoutSeconds = 0 };

        // Act
        var action = () => options.Validate();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*TimeoutSeconds*");
    }

    [Fact]
    public void Validate_ShouldThrow_WhenTotalTimeoutLessThanTimeout()
    {
        // Arrange
        var options = new HandoffOptions
        {
            TimeoutSeconds = 60,
            TotalTimeoutSeconds = 30,
        };

        // Act
        var action = () => options.Validate();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*TotalTimeoutSeconds*");
    }

    [Fact]
    public void Options_ShouldBeConfigurable()
    {
        // Act
        var options = new HandoffOptions
        {
            MaxHandoffDepth = 10,
            TimeoutSeconds = 120,
            TotalTimeoutSeconds = 600,
            AllowCycles = true,
            FallbackToSource = false,
        };

        // Assert
        options.MaxHandoffDepth.Should().Be(10);
        options.TimeoutSeconds.Should().Be(120);
        options.TotalTimeoutSeconds.Should().Be(600);
        options.AllowCycles.Should().BeTrue();
        options.FallbackToSource.Should().BeFalse();
    }
}
