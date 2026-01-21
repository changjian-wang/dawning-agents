namespace Dawning.Agents.Tests.Orchestration;

using Dawning.Agents.Abstractions.Orchestration;
using FluentAssertions;
using Xunit;

/// <summary>
/// OrchestratorOptions 单元测试
/// </summary>
public class OrchestratorOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Act
        var options = new OrchestratorOptions();

        // Assert
        options.MaxConcurrency.Should().Be(5);
        options.TimeoutSeconds.Should().Be(300);
        options.AgentTimeoutSeconds.Should().Be(60);
        options.ContinueOnError.Should().BeFalse();
        options.AggregationStrategy.Should().Be(ResultAggregationStrategy.LastResult);
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        // Assert
        OrchestratorOptions.SectionName.Should().Be("Orchestration");
    }

    [Fact]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        // Arrange
        var options = new OrchestratorOptions
        {
            MaxConcurrency = 10,
            TimeoutSeconds = 120,
            AgentTimeoutSeconds = 30,
        };

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMaxConcurrency_Throws(int concurrency)
    {
        // Arrange
        var options = new OrchestratorOptions { MaxConcurrency = concurrency };

        // Act & Assert
        options
            .Invoking(o => o.Validate())
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*MaxConcurrency*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidTimeoutSeconds_Throws(int timeout)
    {
        // Arrange
        var options = new OrchestratorOptions { TimeoutSeconds = timeout };

        // Act & Assert
        options
            .Invoking(o => o.Validate())
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*TimeoutSeconds*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidAgentTimeoutSeconds_Throws(int timeout)
    {
        // Arrange
        var options = new OrchestratorOptions { AgentTimeoutSeconds = timeout };

        // Act & Assert
        options
            .Invoking(o => o.Validate())
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*AgentTimeoutSeconds*");
    }
}
