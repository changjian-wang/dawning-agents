using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Core.Validation;
using FluentAssertions;

namespace Dawning.Agents.Tests.Validation;

public class AgentOptionsValidatorTests
{
    private readonly AgentOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithValidOptions_ShouldPass()
    {
        // Arrange
        var options = new AgentOptions
        {
            Name = "TestAgent",
            Instructions = "You are a helpful assistant.",
            MaxSteps = 10,
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldFail()
    {
        // Arrange
        var options = new AgentOptions { Name = "", MaxSteps = 10 };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WithTooLongName_ShouldFail()
    {
        // Arrange
        var options = new AgentOptions { Name = new string('x', 101), MaxSteps = 10 };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WithEmptyInstructions_ShouldFail()
    {
        // Arrange
        var options = new AgentOptions
        {
            Name = "TestAgent",
            Instructions = "",
            MaxSteps = 10,
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Instructions");
    }

    [Fact]
    public void Validate_WithZeroMaxSteps_ShouldFail()
    {
        // Arrange
        var options = new AgentOptions { Name = "TestAgent", MaxSteps = 0 };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxSteps");
    }

    [Fact]
    public void Validate_WithNegativeMaxSteps_ShouldFail()
    {
        // Arrange
        var options = new AgentOptions { Name = "TestAgent", MaxSteps = -1 };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxSteps");
    }

    [Fact]
    public void Validate_WithTooLargeMaxSteps_ShouldFail()
    {
        // Arrange
        var options = new AgentOptions { Name = "TestAgent", MaxSteps = 101 };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxSteps");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_WithValidMaxSteps_ShouldPass(int maxSteps)
    {
        // Arrange
        var options = new AgentOptions { Name = "TestAgent", MaxSteps = maxSteps };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMaxTokens_ShouldFail(int maxTokens)
    {
        // Arrange
        var options = new AgentOptions { Name = "TestAgent", MaxTokens = maxTokens };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxTokens");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMaxCostPerRun_ShouldFail(int cost)
    {
        // Arrange
        var options = new AgentOptions { Name = "TestAgent", MaxCostPerRun = cost };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxCostPerRun");
    }

    [Fact]
    public void Validate_WithNullMaxCostPerRun_ShouldPass()
    {
        // Arrange
        var options = new AgentOptions { Name = "TestAgent", MaxCostPerRun = null };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
