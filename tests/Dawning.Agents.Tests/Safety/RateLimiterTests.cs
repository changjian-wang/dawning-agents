using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core.Safety;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Dawning.Agents.Tests.Safety;

public class SlidingWindowRateLimiterTests
{
    private static IOptions<RateLimitOptions> CreateOptions(
        int maxRequests = 10,
        TimeSpan? windowSize = null,
        bool enabled = true
    )
    {
        return Options.Create(
            new RateLimitOptions
            {
                MaxRequestsPerWindow = maxRequests,
                WindowSize = windowSize ?? TimeSpan.FromMinutes(1),
                Enabled = enabled,
            }
        );
    }

    [Fact]
    public async Task TryAcquireAsync_WhenDisabled_ShouldAlwaysAllow()
    {
        // Arrange
        var options = CreateOptions(enabled: false);
        var limiter = new SlidingWindowRateLimiter(options);

        // Act - Try many times
        for (int i = 0; i < 100; i++)
        {
            var result = await limiter.TryAcquireAsync("test-key");

            // Assert
            result.IsAllowed.Should().BeTrue();
        }
    }

    [Fact]
    public async Task TryAcquireAsync_WithinLimit_ShouldAllow()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 5);
        var limiter = new SlidingWindowRateLimiter(options);

        // Act
        for (int i = 0; i < 5; i++)
        {
            var result = await limiter.TryAcquireAsync("test-key");

            // Assert
            result.IsAllowed.Should().BeTrue();
            result.RemainingRequests.Should().Be(5 - i - 1);
        }
    }

    [Fact]
    public async Task TryAcquireAsync_ExceedingLimit_ShouldDeny()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 3);
        var limiter = new SlidingWindowRateLimiter(options);

        // Act - Use up all requests
        for (int i = 0; i < 3; i++)
        {
            await limiter.TryAcquireAsync("test-key");
        }

        // Act - Try one more
        var result = await limiter.TryAcquireAsync("test-key");

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.RemainingRequests.Should().Be(0);
        result.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_ShouldTrackSeparately()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 2);
        var limiter = new SlidingWindowRateLimiter(options);

        // Act - Use up key1
        await limiter.TryAcquireAsync("key1");
        await limiter.TryAcquireAsync("key1");

        // Act - key2 should still be available
        var result = await limiter.TryAcquireAsync("key2");

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(1);
    }

    [Fact]
    public async Task TryAcquireAsync_AfterWindowExpires_ShouldAllowAgain()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var options = CreateOptions(maxRequests: 2, windowSize: TimeSpan.FromSeconds(10));
        var limiter = new SlidingWindowRateLimiter(options, timeProvider: fakeTime);

        // Use up all requests
        await limiter.TryAcquireAsync("test-key");
        await limiter.TryAcquireAsync("test-key");

        var deniedResult = await limiter.TryAcquireAsync("test-key");
        deniedResult.IsAllowed.Should().BeFalse();

        // Advance time past window
        fakeTime.Advance(TimeSpan.FromSeconds(11));

        // Act
        var result = await limiter.TryAcquireAsync("test-key");

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatus_ShouldReturnCurrentState()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 10);
        var limiter = new SlidingWindowRateLimiter(options);

        // Act
        await limiter.TryAcquireAsync("test-key");
        await limiter.TryAcquireAsync("test-key");
        await limiter.TryAcquireAsync("test-key");

        var status = limiter.GetStatus("test-key");

        // Assert
        status.CurrentCount.Should().Be(3);
        status.MaxRequests.Should().Be(10);
        status.IsLimited.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatus_WhenLimited_ShouldIndicateLimited()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 2);
        var limiter = new SlidingWindowRateLimiter(options);

        // Act
        await limiter.TryAcquireAsync("test-key");
        await limiter.TryAcquireAsync("test-key");

        var status = limiter.GetStatus("test-key");

        // Assert
        status.IsLimited.Should().BeTrue();
    }

    [Fact]
    public async Task Reset_ShouldClearCounter()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 2);
        var limiter = new SlidingWindowRateLimiter(options);

        await limiter.TryAcquireAsync("test-key");
        await limiter.TryAcquireAsync("test-key");

        // Act
        limiter.Reset("test-key");

        var status = limiter.GetStatus("test-key");

        // Assert
        status.CurrentCount.Should().Be(0);
        status.IsLimited.Should().BeFalse();
    }
}

public class TokenRateLimiterTests
{
    private static IOptions<RateLimitOptions> CreateOptions(
        int maxPerRequest = 1000,
        int maxPerSession = 10000,
        bool enabled = true
    )
    {
        return Options.Create(
            new RateLimitOptions
            {
                MaxTokensPerRequest = maxPerRequest,
                MaxTokensPerSession = maxPerSession,
                Enabled = enabled,
            }
        );
    }

    [Fact]
    public void TryUseTokens_WhenDisabled_ShouldAlwaysAllow()
    {
        // Arrange
        var options = CreateOptions(enabled: false);
        var limiter = new TokenRateLimiter(options);

        // Act
        var result = limiter.TryUseTokens("session1", 100000);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void TryUseTokens_WithinLimits_ShouldAllow()
    {
        // Arrange
        var options = CreateOptions(maxPerRequest: 500, maxPerSession: 1000);
        var limiter = new TokenRateLimiter(options);

        // Act
        var result = limiter.TryUseTokens("session1", 400);

        // Assert
        result.Should().BeTrue();
        limiter.GetUsedTokens("session1").Should().Be(400);
    }

    [Fact]
    public void TryUseTokens_ExceedingPerRequest_ShouldDeny()
    {
        // Arrange
        var options = CreateOptions(maxPerRequest: 500, maxPerSession: 10000);
        var limiter = new TokenRateLimiter(options);

        // Act
        var result = limiter.TryUseTokens("session1", 600);

        // Assert
        result.Should().BeFalse();
        limiter.GetUsedTokens("session1").Should().Be(0);
    }

    [Fact]
    public void TryUseTokens_ExceedingPerSession_ShouldDeny()
    {
        // Arrange
        var options = CreateOptions(maxPerRequest: 500, maxPerSession: 1000);
        var limiter = new TokenRateLimiter(options);

        // Use up most of the session
        limiter.TryUseTokens("session1", 400);
        limiter.TryUseTokens("session1", 400);

        // Act - Try to use more than remaining
        var result = limiter.TryUseTokens("session1", 300);

        // Assert
        result.Should().BeFalse();
        limiter.GetUsedTokens("session1").Should().Be(800);
    }

    [Fact]
    public void TryUseTokens_DifferentSessions_ShouldTrackSeparately()
    {
        // Arrange
        var options = CreateOptions(maxPerRequest: 500, maxPerSession: 1000);
        var limiter = new TokenRateLimiter(options);

        // Act
        limiter.TryUseTokens("session1", 500);
        limiter.TryUseTokens("session2", 300);

        // Assert
        limiter.GetUsedTokens("session1").Should().Be(500);
        limiter.GetUsedTokens("session2").Should().Be(300);
    }

    [Fact]
    public void ResetSession_ShouldClearTokenCount()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);

        limiter.TryUseTokens("session1", 500);

        // Act
        limiter.ResetSession("session1");

        // Assert
        limiter.GetUsedTokens("session1").Should().Be(0);
    }
}
