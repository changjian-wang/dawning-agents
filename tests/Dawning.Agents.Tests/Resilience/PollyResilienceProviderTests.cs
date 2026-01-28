using Dawning.Agents.Abstractions.Resilience;
using Dawning.Agents.Core.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.Resilience;

public class PollyResilienceProviderTests
{
    private readonly IOptions<ResilienceOptions> _defaultOptions;

    public PollyResilienceProviderTests()
    {
        _defaultOptions = Options.Create(new ResilienceOptions());
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var provider = new PollyResilienceProvider(_defaultOptions);
        var expected = 42;

        // Act
        var result = await provider.ExecuteAsync(async ct =>
        {
            await Task.Delay(1, ct);
            return expected;
        });

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ExecuteAsync_WithVoidOperation_CompletesSuccessfully()
    {
        // Arrange
        var provider = new PollyResilienceProvider(_defaultOptions);
        var executed = false;

        // Act
        await provider.ExecuteAsync(async ct =>
        {
            await Task.Delay(1, ct);
            executed = true;
        });

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithRetryableException_RetriesAndSucceeds()
    {
        // Arrange
        var options = Options.Create(
            new ResilienceOptions
            {
                Retry = new RetryOptions
                {
                    MaxRetryAttempts = 3,
                    BaseDelayMs = 10,
                    UseJitter = false,
                },
                CircuitBreaker = new CircuitBreakerOptions { Enabled = false },
                Timeout = new TimeoutOptions { Enabled = false },
            }
        );

        var provider = new PollyResilienceProvider(options);
        var attemptCount = 0;

        // Act
        var result = await provider.ExecuteAsync<int>(async ct =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new HttpRequestException("Simulated failure");
            }
            return 42;
        });

        // Assert
        result.Should().Be(42);
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var provider = new PollyResilienceProvider(_defaultOptions);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await provider.ExecuteAsync(
                    async ct =>
                    {
                        ct.ThrowIfCancellationRequested();
                        await Task.Delay(100, ct);
                        return 1;
                    },
                    cts.Token
                )
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithNonRetryableException_ThrowsImmediately()
    {
        // Arrange
        var options = Options.Create(
            new ResilienceOptions
            {
                Retry = new RetryOptions { MaxRetryAttempts = 3, BaseDelayMs = 10 },
                CircuitBreaker = new CircuitBreakerOptions { Enabled = false },
                Timeout = new TimeoutOptions { Enabled = false },
            }
        );

        var provider = new PollyResilienceProvider(options);
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await provider.ExecuteAsync<int>(async ct =>
            {
                attemptCount++;
                throw new InvalidOperationException("Non-retryable");
            });
        });

        // 非重试异常只执行一次
        attemptCount.Should().Be(1);
    }
}

public class ResilienceOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveCorrectValues()
    {
        // Arrange & Act
        var options = new ResilienceOptions();

        // Assert
        options.Retry.Should().NotBeNull();
        options.Retry.Enabled.Should().BeTrue();
        options.Retry.MaxRetryAttempts.Should().Be(3);
        options.Retry.BaseDelayMs.Should().Be(1000);
        options.Retry.UseJitter.Should().BeTrue();
        options.Retry.MaxDelayMs.Should().Be(30000);

        options.CircuitBreaker.Should().NotBeNull();
        options.CircuitBreaker.Enabled.Should().BeTrue();
        options.CircuitBreaker.FailureRatio.Should().Be(0.5);
        options.CircuitBreaker.SamplingDurationSeconds.Should().Be(30);
        options.CircuitBreaker.MinimumThroughput.Should().Be(10);
        options.CircuitBreaker.BreakDurationSeconds.Should().Be(30);

        options.Timeout.Should().NotBeNull();
        options.Timeout.Enabled.Should().BeTrue();
        options.Timeout.TimeoutSeconds.Should().Be(120);
    }

    [Fact]
    public void SectionName_IsResilience()
    {
        ResilienceOptions.SectionName.Should().Be("Resilience");
    }
}
