using Dawning.Agents.Abstractions.Resilience;
using Dawning.Agents.Core.Validation;
using FluentAssertions;

namespace Dawning.Agents.Tests.Validation;

public class ResilienceOptionsValidatorTests
{
    private readonly ResilienceOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithDefaultOptions_ShouldPass()
    {
        // Arrange
        var options = new ResilienceOptions();

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNegativeMaxRetryAttempts_ShouldFail()
    {
        // Arrange
        var options = new ResilienceOptions { Retry = new RetryOptions { MaxRetryAttempts = -1 } };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Retry.MaxRetryAttempts");
    }

    [Fact]
    public void Validate_WithTooLargeMaxRetryAttempts_ShouldFail()
    {
        // Arrange
        var options = new ResilienceOptions { Retry = new RetryOptions { MaxRetryAttempts = 11 } };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Retry.MaxRetryAttempts");
    }

    [Fact]
    public void Validate_WithZeroBaseDelayMs_ShouldFail()
    {
        // Arrange
        var options = new ResilienceOptions { Retry = new RetryOptions { BaseDelayMs = 0 } };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Retry.BaseDelayMs");
    }

    [Fact]
    public void Validate_WithMaxDelayLessThanBase_ShouldFail()
    {
        // Arrange
        var options = new ResilienceOptions
        {
            Retry = new RetryOptions { BaseDelayMs = 5000, MaxDelayMs = 1000 },
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Retry.MaxDelayMs");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_WithInvalidFailureRatio_ShouldFail(double ratio)
    {
        // Arrange
        var options = new ResilienceOptions
        {
            CircuitBreaker = new CircuitBreakerOptions { FailureRatio = ratio },
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CircuitBreaker.FailureRatio");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_WithValidFailureRatio_ShouldPass(double ratio)
    {
        // Arrange
        var options = new ResilienceOptions
        {
            CircuitBreaker = new CircuitBreakerOptions { FailureRatio = ratio },
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithZeroTimeoutSeconds_ShouldFail()
    {
        // Arrange
        var options = new ResilienceOptions { Timeout = new TimeoutOptions { TimeoutSeconds = 0 } };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Timeout.TimeoutSeconds");
    }

    [Fact]
    public void Validate_WithTooLargeTimeoutSeconds_ShouldFail()
    {
        // Arrange
        var options = new ResilienceOptions
        {
            Timeout = new TimeoutOptions { TimeoutSeconds = 601 },
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Timeout.TimeoutSeconds");
    }
}
