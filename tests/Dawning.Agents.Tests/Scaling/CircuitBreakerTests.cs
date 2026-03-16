namespace Dawning.Agents.Tests.Scaling;

using Dawning.Agents.Abstractions.Scaling;
using Dawning.Agents.Core.Scaling;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

public class CircuitBreakerTests
{
    [Fact]
    public void InitialState_IsClosed()
    {
        var breaker = new CircuitBreaker();

        breaker.State.Should().Be(CircuitState.Closed);
        breaker.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulCall_KeepsCircuitClosed()
    {
        var breaker = new CircuitBreaker();

        var result = await breaker.ExecuteAsync(() => Task.FromResult(42));

        result.Should().Be(42);
        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task ExecuteAsync_FailuresUnderThreshold_KeepsCircuitClosed()
    {
        var breaker = new CircuitBreaker(failureThreshold: 3);

        // 2 failures (under threshold of 3)
        for (int i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("fail"))
            );
        }

        breaker.State.Should().Be(CircuitState.Closed);
        breaker.FailureCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_FailuresAtThreshold_OpensCircuit()
    {
        var breaker = new CircuitBreaker(failureThreshold: 3);

        for (int i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("fail"))
            );
        }

        breaker.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOpen_ThrowsCircuitBreakerOpenException()
    {
        var breaker = new CircuitBreaker(failureThreshold: 1);

        // Open the circuit
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("fail"))
        );

        // Next call should throw CircuitBreakerOpenException
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            breaker.ExecuteAsync(() => Task.FromResult(42))
        );
    }

    [Fact]
    public async Task ExecuteAsync_AfterResetTimeout_TransitionsToHalfOpen()
    {
        var fakeTime = new FakeTimeProvider();
        var breaker = new CircuitBreaker(
            failureThreshold: 1,
            resetTimeout: TimeSpan.FromMilliseconds(50),
            timeProvider: fakeTime
        );

        // Open the circuit
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("fail"))
        );

        breaker.State.Should().Be(CircuitState.Open);

        // Advance time past reset timeout
        fakeTime.Advance(TimeSpan.FromMilliseconds(100));

        // Trigger state check by calling ExecuteAsync (which checks GetCurrentState)
        var result = await breaker.ExecuteAsync(() => Task.FromResult(42));
        result.Should().Be(42);
        breaker.State.Should().Be(CircuitState.Closed); // Successful call closes circuit
    }

    [Fact]
    public async Task ExecuteAsync_SuccessInHalfOpen_ClosesCircuit()
    {
        var fakeTime = new FakeTimeProvider();
        var breaker = new CircuitBreaker(
            failureThreshold: 1,
            resetTimeout: TimeSpan.FromMilliseconds(50),
            timeProvider: fakeTime
        );

        // Open the circuit
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("fail"))
        );

        // Advance time to half-open
        fakeTime.Advance(TimeSpan.FromMilliseconds(100));

        // Successful call in half-open state
        var result = await breaker.ExecuteAsync(() => Task.FromResult(42));

        result.Should().Be(42);
        breaker.State.Should().Be(CircuitState.Closed);
        breaker.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_VoidAction_WorksCorrectly()
    {
        var breaker = new CircuitBreaker();
        var executed = false;

        await breaker.ExecuteAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Reset_ClearsFailuresAndClosesCircuit()
    {
        var breaker = new CircuitBreaker(failureThreshold: 1);

        // Open the circuit
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("fail"))
        );

        breaker.Reset();

        breaker.State.Should().Be(CircuitState.Closed);
        breaker.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task SuccessfulCall_ResetsFailureCount()
    {
        var breaker = new CircuitBreaker(failureThreshold: 3);

        // Some failures
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("fail"))
        );
        breaker.FailureCount.Should().Be(1);

        // Successful call
        await breaker.ExecuteAsync(() => Task.FromResult(42));

        breaker.FailureCount.Should().Be(0);
    }

    [Fact]
    public void CircuitBreakerOpenException_ParameterlessConstructor_Works()
    {
        var ex = new CircuitBreakerOpenException();

        ex.Message.Should().NotBeEmpty();
    }

    [Fact]
    public void CircuitBreakerOpenException_InnerException_IsPreserved()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new CircuitBreakerOpenException("outer", inner);

        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Constructor_ZeroThreshold_Throws()
    {
        var act = () => new CircuitBreaker(failureThreshold: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativeThreshold_Throws()
    {
        var act = () => new CircuitBreaker(failureThreshold: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ZeroResetTimeout_Throws()
    {
        var act = () => new CircuitBreaker(resetTimeout: TimeSpan.Zero);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativeResetTimeout_Throws()
    {
        var act = () => new CircuitBreaker(resetTimeout: TimeSpan.FromSeconds(-1));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task HalfOpen_OnlyAllowsOneTrialRequest()
    {
        var fakeTime = new FakeTimeProvider();
        var breaker = new CircuitBreaker(
            failureThreshold: 1,
            resetTimeout: TimeSpan.FromSeconds(10),
            timeProvider: fakeTime
        );

        // Open the circuit
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("fail"))
        );
        breaker.State.Should().Be(CircuitState.Open);

        // Transition to HalfOpen
        fakeTime.Advance(TimeSpan.FromSeconds(11));

        // First request in HalfOpen: start a slow trial that we can control
        var trialStarted = new TaskCompletionSource();
        var trialGate = new TaskCompletionSource();
        var trialTask = Task.Run(async () =>
        {
            await breaker.ExecuteAsync(async () =>
            {
                trialStarted.SetResult();
                await trialGate.Task;
                return 42;
            });
        });

        // Wait for trial to actually enter ExecuteAsync
        await trialStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Second concurrent request should be rejected
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            breaker.ExecuteAsync(() => Task.FromResult(99))
        );

        // Let the trial complete successfully
        trialGate.SetResult();
        await trialTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Circuit should be closed now
        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task HalfOpen_FailedTrial_AllowsRetryAfterTimeout()
    {
        var fakeTime = new FakeTimeProvider();
        var breaker = new CircuitBreaker(
            failureThreshold: 1,
            resetTimeout: TimeSpan.FromSeconds(10),
            timeProvider: fakeTime
        );

        // Open the circuit
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("fail"))
        );

        // Transition to HalfOpen
        fakeTime.Advance(TimeSpan.FromSeconds(11));

        // Trial request fails → circuit opens again
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("still bad"))
        );
        breaker.State.Should().Be(CircuitState.Open);

        // Wait for another reset timeout
        fakeTime.Advance(TimeSpan.FromSeconds(11));

        // New trial should be allowed
        var result = await breaker.ExecuteAsync(() => Task.FromResult(42));
        result.Should().Be(42);
        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task HalfOpen_CancelledTrial_AllowsNewTrial()
    {
        var fakeTime = new FakeTimeProvider();
        var breaker = new CircuitBreaker(
            failureThreshold: 1,
            resetTimeout: TimeSpan.FromSeconds(10),
            timeProvider: fakeTime
        );

        // Open the circuit
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("fail"))
        );

        // Transition to HalfOpen
        fakeTime.Advance(TimeSpan.FromSeconds(11));

        // Trial is cancelled
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            breaker.ExecuteAsync(() => Task.FromResult(42), cts.Token)
        );

        // _halfOpenTrialActive should be cleared, new trial should be allowed
        breaker.State.Should().Be(CircuitState.HalfOpen);
        var result = await breaker.ExecuteAsync(() => Task.FromResult(99));
        result.Should().Be(99);
        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task Reset_ClearsHalfOpenTrialActive()
    {
        var fakeTime = new FakeTimeProvider();
        var breaker = new CircuitBreaker(
            failureThreshold: 1,
            resetTimeout: TimeSpan.FromSeconds(10),
            timeProvider: fakeTime
        );

        // Open → HalfOpen
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<int>(() => throw new InvalidOperationException("fail"))
        );
        fakeTime.Advance(TimeSpan.FromSeconds(11));

        // Reset while in HalfOpen
        breaker.Reset();
        breaker.State.Should().Be(CircuitState.Closed);

        // Should work normally (no stale _halfOpenTrialActive blocking)
        var result = await breaker.ExecuteAsync(() => Task.FromResult(42));
        result.Should().Be(42);
    }
}
