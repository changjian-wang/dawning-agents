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

    [Fact]
    public async Task TryAcquireAsync_WhenMaxBucketsReached_ShouldDenyNewKeys()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 100);
        options.Value.MaxBuckets = 2;
        var limiter = new SlidingWindowRateLimiter(options);

        // Fill up buckets
        await limiter.TryAcquireAsync("key1");
        await limiter.TryAcquireAsync("key2");

        // Act - Try a new key
        var result = await limiter.TryAcquireAsync("key3");

        // Assert
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireAsync_WithPolicy_ShouldUseNamedPolicyLimits()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 2);
        options.Value.Policies = new Dictionary<string, RateLimitPolicy>
        {
            ["premium"] = new() { MaxRequestsPerWindow = 100, WindowSize = TimeSpan.FromMinutes(1) },
        };
        var limiter = new SlidingWindowRateLimiter(options);

        // Act - The default would deny at 3rd, but premium allows 100
        for (int i = 0; i < 5; i++)
        {
            var result = await limiter.TryAcquireAsync("vip-user", "premium");
            result.IsAllowed.Should().BeTrue();
        }
    }

    [Fact]
    public async Task TryAcquireAsync_WithUnknownPolicy_ShouldUseDefaultLimits()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 2);
        var limiter = new SlidingWindowRateLimiter(options);

        await limiter.TryAcquireAsync("key1", "nonexistent");
        await limiter.TryAcquireAsync("key1", "nonexistent");

        // Act
        var result = await limiter.TryAcquireAsync("key1", "nonexistent");

        // Assert - Falls back to default (max=2)
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireAsync_WithBackpressure_ShouldWaitAndRetry()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var options = CreateOptions(maxRequests: 1, windowSize: TimeSpan.FromSeconds(2));
        options.Value.EnableBackpressure = true;
        options.Value.BackpressureTimeout = TimeSpan.FromSeconds(5);
        var limiter = new SlidingWindowRateLimiter(options, timeProvider: fakeTime);

        // Use up the limit
        await limiter.TryAcquireAsync("key1");

        // Act - Start backpressure acquire in background, then advance time
        var acquireTask = Task.Run(async () =>
        {
            // This should block until time advances
            return await limiter.TryAcquireAsync("key1");
        });

        // Give the task a moment to start waiting
        await Task.Delay(50);

        // Advance time past window — enough for the bucket to expire
        fakeTime.Advance(TimeSpan.FromSeconds(3));

        var result = await acquireTask;

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_WithBackpressureTimeout_ShouldDenyAfterTimeout()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var options = CreateOptions(maxRequests: 1, windowSize: TimeSpan.FromSeconds(60));
        options.Value.EnableBackpressure = true;
        options.Value.BackpressureTimeout = TimeSpan.FromSeconds(1);
        var limiter = new SlidingWindowRateLimiter(options, timeProvider: fakeTime);

        // Use up the limit
        await limiter.TryAcquireAsync("key1");

        // Advance time past backpressure timeout but not past window
        fakeTime.Advance(TimeSpan.FromSeconds(2));

        // Act - This should timeout immediately since time already past deadline
        var result = await limiter.TryAcquireAsync("key1");

        // Assert
        result.IsAllowed.Should().BeFalse();
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

public class RateLimitOptionsTests
{
    [Fact]
    public void Default_Options_Should_Have_Expected_Values()
    {
        var options = new RateLimitOptions();

        options.MaxRequestsPerWindow.Should().Be(60);
        options.WindowSize.Should().Be(TimeSpan.FromMinutes(1));
        options.MaxTokensPerSession.Should().Be(100000);
        options.MaxTokensPerRequest.Should().Be(4000);
        options.Enabled.Should().BeTrue();
        RateLimitOptions.SectionName.Should().Be("RateLimit");
    }

    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        var options = new RateLimitOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ZeroMaxRequestsPerWindow_Throws()
    {
        var options = new RateLimitOptions { MaxRequestsPerWindow = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxRequestsPerWindow*");
    }

    [Fact]
    public void Validate_ZeroWindowSize_Throws()
    {
        var options = new RateLimitOptions { WindowSize = TimeSpan.Zero };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*WindowSize*");
    }

    [Fact]
    public void Validate_ZeroMaxTokensPerSession_Throws()
    {
        var options = new RateLimitOptions { MaxTokensPerSession = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxTokensPerSession*");
    }

    [Fact]
    public void Validate_ZeroMaxTokensPerRequest_Throws()
    {
        var options = new RateLimitOptions { MaxTokensPerRequest = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxTokensPerRequest*");
    }

    [Fact]
    public void Validate_ZeroMaxBuckets_Throws()
    {
        var options = new RateLimitOptions { MaxBuckets = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxBuckets*");
    }

    [Fact]
    public void Default_Options_Should_Include_NewProperties()
    {
        var options = new RateLimitOptions();

        options.MaxBuckets.Should().Be(10_000);
        options.EnableBackpressure.Should().BeFalse();
        options.BackpressureTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.Policies.Should().BeEmpty();
    }
}
