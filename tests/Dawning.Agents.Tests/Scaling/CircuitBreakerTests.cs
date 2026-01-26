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
}
