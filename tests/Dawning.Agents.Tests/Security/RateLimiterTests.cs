using System;
using System.Threading.Tasks;
using Dawning.Agents.Core.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dawning.Agents.Tests.Security;

public class SlidingWindowRateLimiterTests
{
    private static IOptions<RateLimitOptions> CreateOptions(
        bool enabled = true,
        int requestsPerMinute = 60,
        int windowSizeSeconds = 60
    )
    {
        return Options.Create(
            new RateLimitOptions
            {
                Enabled = enabled,
                DefaultRequestsPerMinute = requestsPerMinute,
                WindowSizeSeconds = windowSizeSeconds,
            }
        );
    }

    [Fact]
    public async Task CheckAsync_WhenDisabled_ShouldAlwaysAllow()
    {
        // Arrange
        var limiter = new SlidingWindowRateLimiter(CreateOptions(enabled: false));

        // Act
        var result = await limiter.CheckAsync("user1");

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task CheckAsync_FirstRequest_ShouldAllow()
    {
        // Arrange
        var limiter = new SlidingWindowRateLimiter(CreateOptions(requestsPerMinute: 10));

        // Act
        var result = await limiter.CheckAsync("user1");

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(9);
    }

    [Fact]
    public async Task CheckAsync_WithinLimit_ShouldAllow()
    {
        // Arrange
        var limiter = new SlidingWindowRateLimiter(CreateOptions(requestsPerMinute: 5));

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            var result = await limiter.CheckAsync("user1");
            result.IsAllowed.Should().BeTrue();
            result.RemainingRequests.Should().Be(5 - i - 1);
        }
    }

    [Fact]
    public async Task CheckAsync_ExceedsLimit_ShouldDeny()
    {
        // Arrange
        var limiter = new SlidingWindowRateLimiter(CreateOptions(requestsPerMinute: 3));

        // Exhaust the limit
        for (int i = 0; i < 3; i++)
        {
            await limiter.CheckAsync("user1");
        }

        // Act
        var result = await limiter.CheckAsync("user1");

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.RemainingRequests.Should().Be(0);
        result.RetryAfter.Should().NotBeNull();
        result.Reason.Should().Contain("Rate limit exceeded");
    }

    [Fact]
    public async Task CheckAsync_DifferentKeys_ShouldHaveSeparateLimits()
    {
        // Arrange
        var limiter = new SlidingWindowRateLimiter(CreateOptions(requestsPerMinute: 2));

        // Exhaust user1's limit
        await limiter.CheckAsync("user1");
        await limiter.CheckAsync("user1");
        var user1Denied = await limiter.CheckAsync("user1");

        // Act - user2 should still be allowed
        var user2Allowed = await limiter.CheckAsync("user2");

        // Assert
        user1Denied.IsAllowed.Should().BeFalse();
        user2Allowed.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithCustomLimit_ShouldUseCustomLimit()
    {
        // Arrange
        var limiter = new SlidingWindowRateLimiter(CreateOptions(requestsPerMinute: 100));

        // Use custom limit of 2
        await limiter.CheckAsync("user1", limit: 2);
        await limiter.CheckAsync("user1", limit: 2);

        // Act
        var result = await limiter.CheckAsync("user1", limit: 2);

        // Assert
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task ResetAsync_ShouldClearBucket()
    {
        // Arrange
        var limiter = new SlidingWindowRateLimiter(CreateOptions(requestsPerMinute: 2));

        // Exhaust the limit
        await limiter.CheckAsync("user1");
        await limiter.CheckAsync("user1");
        var denied = await limiter.CheckAsync("user1");
        denied.IsAllowed.Should().BeFalse();

        // Act
        await limiter.ResetAsync("user1");
        var afterReset = await limiter.CheckAsync("user1");

        // Assert
        afterReset.IsAllowed.Should().BeTrue();
        afterReset.RemainingRequests.Should().Be(1);
    }

    [Fact]
    public async Task ResetAsync_NonExistentKey_ShouldNotThrow()
    {
        // Arrange
        var limiter = new SlidingWindowRateLimiter(CreateOptions());

        // Act & Assert
        var act = async () => await limiter.ResetAsync("non-existent");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CheckAsync_ConcurrentRequests_ShouldBeThreadSafe()
    {
        // Arrange
        var limiter = new SlidingWindowRateLimiter(CreateOptions(requestsPerMinute: 100));
        var tasks = new Task<RateLimitResult>[50];

        // Act
        for (int i = 0; i < 50; i++)
        {
            tasks[i] = limiter.CheckAsync("concurrent-user");
        }
        var results = await Task.WhenAll(tasks);

        // Assert
        var allowedCount = results.Count(r => r.IsAllowed);
        allowedCount.Should().Be(50); // All should be allowed with limit of 100
    }

    [Fact]
    public async Task CheckAsync_WithNullOptions_ShouldUseDefaults()
    {
        // Arrange
        var limiter = new SlidingWindowRateLimiter();

        // Act
        var result = await limiter.CheckAsync("user1");

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(59); // Default is 60 per minute
    }
}

public class RateLimitResultTests
{
    [Fact]
    public void Allowed_ShouldCreateAllowedResult()
    {
        // Act
        var result = RateLimitResult.Allowed(10);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(10);
        result.RetryAfter.Should().BeNull();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Denied_ShouldCreateDeniedResult()
    {
        // Act
        var result = RateLimitResult.Denied(TimeSpan.FromSeconds(30), "Too many requests");

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.RemainingRequests.Should().Be(0);
        result.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
        result.Reason.Should().Be("Too many requests");
    }
}

public class RateLimitOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new RateLimitOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.DefaultRequestsPerMinute.Should().Be(60);
        options.WindowSizeSeconds.Should().Be(60);
        options.UseTokenBucket.Should().BeFalse();
        options.TokenBucketCapacity.Should().Be(100);
        options.TokenRefillRatePerSecond.Should().Be(10);
        RateLimitOptions.SectionName.Should().Be("RateLimit");
    }
}
