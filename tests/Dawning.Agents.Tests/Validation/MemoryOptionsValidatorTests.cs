using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Validation;
using FluentAssertions;

namespace Dawning.Agents.Tests.Validation;

public class MemoryOptionsValidatorTests
{
    private readonly MemoryOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithDefaultOptions_ShouldPass()
    {
        // Arrange
        var options = new MemoryOptions();

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidDowngradeThreshold_ShouldFail(int value)
    {
        // Arrange
        var options = new MemoryOptions { DowngradeThreshold = value };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DowngradeThreshold");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidRetrieveTopK_ShouldFail(int value)
    {
        // Arrange
        var options = new MemoryOptions { RetrieveTopK = value };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RetrieveTopK");
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void Validate_WithInvalidMinRelevanceScore_ShouldFail(float value)
    {
        // Arrange
        var options = new MemoryOptions { MinRelevanceScore = value };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MinRelevanceScore");
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public void Validate_WithValidMinRelevanceScore_ShouldPass(float value)
    {
        // Arrange
        var options = new MemoryOptions { MinRelevanceScore = value };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
