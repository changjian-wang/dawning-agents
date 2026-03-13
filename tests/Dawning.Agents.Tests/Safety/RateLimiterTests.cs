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
            result.DenyReason.Should().Be(RateLimitDenyReason.None);
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
            result.DenyReason.Should().Be(RateLimitDenyReason.None);
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
        result.DenyReason.Should().Be(RateLimitDenyReason.RateLimitExceeded);
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
        result.DenyReason.Should().Be(RateLimitDenyReason.None);
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
        deniedResult.DenyReason.Should().Be(RateLimitDenyReason.RateLimitExceeded);

        // Advance time past window
        fakeTime.Advance(TimeSpan.FromSeconds(11));

        // Act
        var result = await limiter.TryAcquireAsync("test-key");

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.DenyReason.Should().Be(RateLimitDenyReason.None);
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
        result.DenyReason.Should().Be(RateLimitDenyReason.BucketCapReached);
    }

    [Fact]
    public async Task TryAcquireAsync_WithPolicy_ShouldUseNamedPolicyLimits()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 2);
        options.Value.Policies = new Dictionary<string, RateLimitPolicy>
        {
            ["premium"] = new()
            {
                MaxRequestsPerWindow = 100,
                WindowSize = TimeSpan.FromMinutes(1),
            },
        };
        var limiter = new SlidingWindowRateLimiter(options);

        // Act - The default would deny at 3rd, but premium allows 100
        for (int i = 0; i < 5; i++)
        {
            var result = await limiter.TryAcquireAsync("vip-user", "premium");
            result.IsAllowed.Should().BeTrue();
            result.DenyReason.Should().Be(RateLimitDenyReason.None);
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
        result.DenyReason.Should().Be(RateLimitDenyReason.RateLimitExceeded);
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
        result.DenyReason.Should().Be(RateLimitDenyReason.None);
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

        // Act - Start backpressure acquire in background, then advance time past timeout
        var acquireTask = Task.Run(async () => await limiter.TryAcquireAsync("key1"));

        // Give the task a moment to enter WaitAndRetryAsync
        await Task.Delay(50);

        // Advance time past backpressure timeout (1s) but not past window (60s)
        fakeTime.Advance(TimeSpan.FromSeconds(2));

        var result = await acquireTask;

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Be(RateLimitDenyReason.RateLimitExceeded);
    }

    [Fact]
    public async Task TryAcquireAsync_ExceedingLimit_ShouldReturnRateLimitExceededReason()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 1);
        var limiter = new SlidingWindowRateLimiter(options);
        await limiter.TryAcquireAsync("key1");

        // Act
        var result = await limiter.TryAcquireAsync("key1");

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Be(RateLimitDenyReason.RateLimitExceeded);
    }

    [Fact]
    public async Task TryAcquireAsync_MaxBucketsReached_ShouldReturnBucketCapReachedReason()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 100);
        options.Value.MaxBuckets = 1;
        var limiter = new SlidingWindowRateLimiter(options);
        await limiter.TryAcquireAsync("key1");

        // Act
        var result = await limiter.TryAcquireAsync("key2");

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Be(RateLimitDenyReason.BucketCapReached);
    }

    [Fact]
    public async Task TryAcquireAsync_Allowed_ShouldReturnNoneDenyReason()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 10);
        var limiter = new SlidingWindowRateLimiter(options);

        // Act
        var result = await limiter.TryAcquireAsync("key1");

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.DenyReason.Should().Be(RateLimitDenyReason.None);
    }

    [Fact]
    public async Task GetStatus_WithPolicy_ShouldUseNamedPolicyMaxRequests()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 2);
        options.Value.Policies = new Dictionary<string, RateLimitPolicy>
        {
            ["premium"] = new()
            {
                MaxRequestsPerWindow = 100,
                WindowSize = TimeSpan.FromMinutes(5),
            },
        };
        var limiter = new SlidingWindowRateLimiter(options);
        await limiter.TryAcquireAsync("vip", "premium");

        // Act
        var status = limiter.GetStatus("vip", "premium");

        // Assert
        status.MaxRequests.Should().Be(100);
        status.CurrentCount.Should().Be(1);
        status.IsLimited.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatus_WithoutPolicy_ShouldUseDefaultMaxRequests()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 2);
        options.Value.Policies = new Dictionary<string, RateLimitPolicy>
        {
            ["premium"] = new()
            {
                MaxRequestsPerWindow = 100,
                WindowSize = TimeSpan.FromMinutes(5),
            },
        };
        var limiter = new SlidingWindowRateLimiter(options);
        await limiter.TryAcquireAsync("vip", "premium");

        // Act - GetStatus without policy falls back to default
        var status = limiter.GetStatus("vip");

        // Assert
        status.MaxRequests.Should().Be(2);
    }

    [Fact]
    public async Task EvictIdleBuckets_ShouldRemoveExpiredBuckets()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var options = CreateOptions(maxRequests: 10, windowSize: TimeSpan.FromSeconds(5));
        var limiter = new SlidingWindowRateLimiter(options, timeProvider: fakeTime);

        // Add a request
        await limiter.TryAcquireAsync("key1");

        // Advance past window so the bucket is empty
        fakeTime.Advance(TimeSpan.FromSeconds(10));

        // Act
        limiter.EvictIdleBuckets();

        // Assert - bucket should be removed
        var status = limiter.GetStatus("key1");
        status.CurrentCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new SlidingWindowRateLimiter(options);

        // Act & Assert
        var act = () => limiter.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldNotThrow()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new SlidingWindowRateLimiter(options);

        // Act & Assert - double dispose
        limiter.Dispose();
        var act = () => limiter.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task TryAcquireAsync_AfterDispose_ShouldThrow()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new SlidingWindowRateLimiter(options);
        limiter.Dispose();

        // Act & Assert
        var act = () => limiter.TryAcquireAsync("key1");
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void GetStatus_AfterDispose_ShouldThrow()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new SlidingWindowRateLimiter(options);
        limiter.Dispose();

        // Act & Assert
        var act = () => limiter.GetStatus("key1");
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Reset_AfterDispose_ShouldThrow()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new SlidingWindowRateLimiter(options);
        limiter.Dispose();

        // Act & Assert
        var act = () => limiter.Reset("key1");
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task TryAcquireAsync_ConcurrentAccess_ShouldRespectLimits()
    {
        // Arrange
        var options = CreateOptions(maxRequests: 50);
        var limiter = new SlidingWindowRateLimiter(options);

        // Act - Fire 100 concurrent requests
        var tasks = Enumerable
            .Range(0, 100)
            .Select(_ => limiter.TryAcquireAsync("concurrent-key"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - Exactly 50 should be allowed
        results.Count(r => r.IsAllowed).Should().Be(50);
        results.Count(r => !r.IsAllowed).Should().Be(50);
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
        result.IsAllowed.Should().BeTrue();
        result.DenyReason.Should().Be(RateLimitDenyReason.None);
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
        result.IsAllowed.Should().BeTrue();
        result.DenyReason.Should().Be(RateLimitDenyReason.None);
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
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Be(RateLimitDenyReason.TokenPerRequestExceeded);
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
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Be(RateLimitDenyReason.TokenPerSessionExceeded);
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

    [Fact]
    public void TryUseTokens_MaxBucketsReached_ShouldDenyNewSession()
    {
        // Arrange
        var options = CreateOptions();
        options.Value.MaxBuckets = 2;
        var limiter = new TokenRateLimiter(options);

        limiter.TryUseTokens("session1", 1);
        limiter.TryUseTokens("session2", 1);

        // Act - 3rd session exceeds MaxBuckets
        var result = limiter.TryUseTokens("session3", 1);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.DenyReason.Should().Be(RateLimitDenyReason.TokenBucketCapReached);
    }

    [Fact]
    public void TryUseTokens_MaxBucketsReached_ShouldAllowExistingSession()
    {
        // Arrange
        var options = CreateOptions();
        options.Value.MaxBuckets = 2;
        var limiter = new TokenRateLimiter(options);

        limiter.TryUseTokens("session1", 1);
        limiter.TryUseTokens("session2", 1);

        // Act - existing session should still work
        var result = limiter.TryUseTokens("session1", 1);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.DenyReason.Should().Be(RateLimitDenyReason.None);
    }

    [Fact]
    public void EvictIdleBuckets_ShouldRemoveIdleSessions()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options, timeProvider: fakeTime);

        limiter.TryUseTokens("session1", 100);

        // Advance past idle threshold (10 minutes)
        fakeTime.Advance(TimeSpan.FromMinutes(11));

        // Act
        limiter.EvictIdleBuckets();

        // Assert
        limiter.GetUsedTokens("session1").Should().Be(0);
    }

    [Fact]
    public void EvictIdleBuckets_ShouldKeepActiveSessions()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options, timeProvider: fakeTime);

        limiter.TryUseTokens("session1", 100);

        // Advance only 5 minutes (under idle threshold)
        fakeTime.Advance(TimeSpan.FromMinutes(5));

        // Act
        limiter.EvictIdleBuckets();

        // Assert
        limiter.GetUsedTokens("session1").Should().Be(100);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);

        // Act & Assert - single dispose
        var act = () => limiter.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldNotThrow()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);

        // Act & Assert - double dispose
        limiter.Dispose();
        var act = () => limiter.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void TryUseTokens_AfterDispose_ShouldThrow()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);
        limiter.Dispose();

        // Act & Assert
        var act = () => limiter.TryUseTokens("session1", 100);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetUsedTokens_AfterDispose_ShouldThrow()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);
        limiter.Dispose();

        // Act & Assert
        var act = () => limiter.GetUsedTokens("session1");
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ResetSession_AfterDispose_ShouldThrow()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);
        limiter.Dispose();

        // Act & Assert
        var act = () => limiter.ResetSession("session1");
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void HasBudget_AfterDispose_ShouldThrow()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);
        limiter.Dispose();

        // Act & Assert
        var act = () => limiter.HasBudget("session1");
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void HasBudget_NewSession_ShouldReturnTrue()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);

        // Act & Assert
        limiter.HasBudget("new-session").Should().BeTrue();
    }

    [Fact]
    public void HasBudget_WithinBudget_ShouldReturnTrue()
    {
        // Arrange
        var options = CreateOptions(maxPerSession: 1000);
        var limiter = new TokenRateLimiter(options);
        limiter.TryUseTokens("session1", 500);

        // Act & Assert
        limiter.HasBudget("session1").Should().BeTrue();
    }

    [Fact]
    public void HasBudget_Exhausted_ShouldReturnFalse()
    {
        // Arrange
        var options = CreateOptions(maxPerRequest: 1000, maxPerSession: 1000);
        var limiter = new TokenRateLimiter(options);
        limiter.TryUseTokens("session1", 1000);

        // Act & Assert
        limiter.HasBudget("session1").Should().BeFalse();
    }

    [Fact]
    public void HasBudget_WhenDisabled_ShouldAlwaysReturnTrue()
    {
        // Arrange
        var options = CreateOptions(enabled: false);
        var limiter = new TokenRateLimiter(options);

        // Act & Assert
        limiter.HasBudget("session1").Should().BeTrue();
    }

    [Fact]
    public void TryUseTokens_ConcurrentAccess_ShouldRespectLimits()
    {
        // Arrange
        var options = CreateOptions(maxPerRequest: 1, maxPerSession: 50);
        var limiter = new TokenRateLimiter(options);

        // Act - Fire 100 concurrent tasks
        var results = Enumerable
            .Range(0, 100)
            .AsParallel()
            .Select(_ => limiter.TryUseTokens("session1", 1))
            .ToArray();

        // Assert - Exactly 50 should be allowed
        results.Count(r => r.IsAllowed).Should().Be(50);
        results.Count(r => !r.IsAllowed).Should().Be(50);
    }

    [Fact]
    public void TryUseTokens_ZeroTokenCount_ShouldThrowArgumentOutOfRange()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);

        // Act & Assert
        var act = () => limiter.TryUseTokens("session1", 0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("tokenCount");
    }

    [Fact]
    public void TryUseTokens_NegativeTokenCount_ShouldThrowArgumentOutOfRange()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);

        // Act & Assert
        var act = () => limiter.TryUseTokens("session1", -5);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("tokenCount");
    }

    [Fact]
    public void TryUseTokens_EmptySessionId_ShouldThrowArgumentException()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);

        // Act & Assert
        var act = () => limiter.TryUseTokens("", 100);
        act.Should().Throw<ArgumentException>().WithParameterName("sessionId");
    }

    [Fact]
    public void TryUseTokens_WhitespaceSessionId_ShouldThrowArgumentException()
    {
        // Arrange
        var options = CreateOptions();
        var limiter = new TokenRateLimiter(options);

        // Act & Assert
        var act = () => limiter.TryUseTokens("   ", 100);
        act.Should().Throw<ArgumentException>().WithParameterName("sessionId");
    }

    [Fact]
    public void TryUseTokens_ExhaustedSession_ShouldNotResetAfterIdleEviction()
    {
        // Arrange — default TokenIdleTimeout is 10 minutes
        var fakeTime = new FakeTimeProvider();
        var options = CreateOptions(maxPerRequest: 1000, maxPerSession: 1000);
        var limiter = new TokenRateLimiter(options, timeProvider: fakeTime);

        // Exhaust the session at t=0
        limiter.TryUseTokens("session1", 1000);

        // Advance 9 minutes (just under idle timeout)
        fakeTime.Advance(TimeSpan.FromMinutes(9));

        // Denied attempt at t=9 updates LastAccessed
        var denied = limiter.TryUseTokens("session1", 1);
        denied.IsAllowed.Should().BeFalse();

        // Advance another 9 minutes (t=18, but only 9 min since last access)
        fakeTime.Advance(TimeSpan.FromMinutes(9));
        limiter.EvictIdleBuckets();

        // Bucket should NOT be evicted because LastAccessed was updated on denied attempt
        limiter.GetUsedTokens("session1").Should().Be(1000);
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
        options.EvictionInterval.Should().Be(TimeSpan.FromSeconds(60));
        options.TokenIdleTimeout.Should().Be(TimeSpan.FromMinutes(10));
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
    public void Validate_PolicyWithZeroMaxRequests_Throws()
    {
        var options = new RateLimitOptions
        {
            Policies = new Dictionary<string, RateLimitPolicy>
            {
                ["bad"] = new() { MaxRequestsPerWindow = 0, WindowSize = TimeSpan.FromMinutes(1) },
            },
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*bad*MaxRequestsPerWindow*");
    }

    [Fact]
    public void Validate_PolicyWithZeroWindowSize_Throws()
    {
        var options = new RateLimitOptions
        {
            Policies = new Dictionary<string, RateLimitPolicy>
            {
                ["bad"] = new() { MaxRequestsPerWindow = 10, WindowSize = TimeSpan.Zero },
            },
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*bad*WindowSize*");
    }

    [Fact]
    public void Validate_ValidPolicy_DoesNotThrow()
    {
        var options = new RateLimitOptions
        {
            Policies = new Dictionary<string, RateLimitPolicy>
            {
                ["premium"] = new()
                {
                    MaxRequestsPerWindow = 100,
                    WindowSize = TimeSpan.FromMinutes(5),
                },
            },
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
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

    [Fact]
    public void Validate_ZeroEvictionInterval_Throws()
    {
        var options = new RateLimitOptions { EvictionInterval = TimeSpan.Zero };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*EvictionInterval*");
    }

    [Fact]
    public void Validate_NegativeEvictionInterval_Throws()
    {
        var options = new RateLimitOptions { EvictionInterval = TimeSpan.FromSeconds(-1) };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*EvictionInterval*");
    }

    [Fact]
    public void Validate_ZeroTokenIdleTimeout_Throws()
    {
        var options = new RateLimitOptions { TokenIdleTimeout = TimeSpan.Zero };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*TokenIdleTimeout*");
    }

    [Fact]
    public void Validate_NegativeTokenIdleTimeout_Throws()
    {
        var options = new RateLimitOptions { TokenIdleTimeout = TimeSpan.FromSeconds(-1) };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*TokenIdleTimeout*");
    }

    [Fact]
    public void Validate_ZeroBackpressureTimeout_WhenEnabled_Throws()
    {
        var options = new RateLimitOptions
        {
            EnableBackpressure = true,
            BackpressureTimeout = TimeSpan.Zero,
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*BackpressureTimeout*");
    }

    [Fact]
    public void Validate_NegativeBackpressureTimeout_WhenEnabled_Throws()
    {
        var options = new RateLimitOptions
        {
            EnableBackpressure = true,
            BackpressureTimeout = TimeSpan.FromSeconds(-1),
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*BackpressureTimeout*");
    }

    [Fact]
    public void Validate_ZeroBackpressureTimeout_WhenDisabled_DoesNotThrow()
    {
        var options = new RateLimitOptions
        {
            EnableBackpressure = false,
            BackpressureTimeout = TimeSpan.Zero,
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MaxTokensPerRequestExceedsPerSession_Throws()
    {
        var options = new RateLimitOptions
        {
            MaxTokensPerRequest = 50000,
            MaxTokensPerSession = 10000,
        };
        var act = () => options.Validate();
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*MaxTokensPerRequest*MaxTokensPerSession*");
    }

    [Fact]
    public void Validate_MaxTokensPerRequestEqualsPerSession_DoesNotThrow()
    {
        var options = new RateLimitOptions
        {
            MaxTokensPerRequest = 10000,
            MaxTokensPerSession = 10000,
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }
}
