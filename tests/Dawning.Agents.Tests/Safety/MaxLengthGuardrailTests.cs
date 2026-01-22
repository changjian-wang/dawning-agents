using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core.Safety;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.Safety;

public class MaxLengthGuardrailTests
{
    [Fact]
    public void Constructor_ShouldThrowForInvalidMaxLength()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MaxLengthGuardrail(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MaxLengthGuardrail(-1));
    }

    [Fact]
    public async Task CheckAsync_WithEmptyContent_ShouldPass()
    {
        // Arrange
        var guardrail = new MaxLengthGuardrail(100);

        // Act
        var result = await guardrail.CheckAsync("");

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithNullContent_ShouldPass()
    {
        // Arrange
        var guardrail = new MaxLengthGuardrail(100);

        // Act
        var result = await guardrail.CheckAsync(null!);

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithContentWithinLimit_ShouldPass()
    {
        // Arrange
        var guardrail = new MaxLengthGuardrail(100);
        var content = new string('a', 50);

        // Act
        var result = await guardrail.CheckAsync(content);

        // Assert
        result.Passed.Should().BeTrue();
        result.ProcessedContent.Should().Be(content);
    }

    [Fact]
    public async Task CheckAsync_WithContentExactlyAtLimit_ShouldPass()
    {
        // Arrange
        var guardrail = new MaxLengthGuardrail(100);
        var content = new string('a', 100);

        // Act
        var result = await guardrail.CheckAsync(content);

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithContentExceedingLimit_ShouldFail()
    {
        // Arrange
        var guardrail = new MaxLengthGuardrail(100);
        var content = new string('a', 101);

        // Act
        var result = await guardrail.CheckAsync(content);

        // Assert
        result.Passed.Should().BeFalse();
        result.TriggeredBy.Should().Be("MaxInputLength");
        result.Issues.Should().HaveCount(1);
        result.Issues[0].Type.Should().Be("LengthExceeded");
    }

    [Fact]
    public void ForInput_ShouldCreateInputGuardrail()
    {
        // Arrange
        var options = Options.Create(new SafetyOptions { MaxInputLength = 500 });

        // Act
        var guardrail = MaxLengthGuardrail.ForInput(options);

        // Assert
        guardrail.Name.Should().Be("MaxInputLength");
        guardrail.Description.Should().Contain("500");
    }

    [Fact]
    public void ForOutput_ShouldCreateOutputGuardrail()
    {
        // Arrange
        var options = Options.Create(new SafetyOptions { MaxOutputLength = 1000 });

        // Act
        var guardrail = MaxLengthGuardrail.ForOutput(options);

        // Assert
        guardrail.Name.Should().Be("MaxOutputLength");
        guardrail.Description.Should().Contain("1000");
    }

    [Fact]
    public void IsEnabled_ShouldAlwaysBeTrue()
    {
        // Arrange
        var guardrail = new MaxLengthGuardrail(100);

        // Assert
        guardrail.IsEnabled.Should().BeTrue();
    }
}
