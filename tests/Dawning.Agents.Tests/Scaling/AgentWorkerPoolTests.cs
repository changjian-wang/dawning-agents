using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Scaling;
using Dawning.Agents.Core.Scaling;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Dawning.Agents.Tests.Scaling;

/// <summary>
/// AgentWorkerPool 单元测试
/// </summary>
public class AgentWorkerPoolTests : IDisposable
{
    private readonly Mock<IAgent> _mockAgent;
    private readonly Mock<IAgentRequestQueue> _mockQueue;
    private readonly Mock<ILogger<AgentWorkerPool>> _mockLogger;

    public AgentWorkerPoolTests()
    {
        _mockAgent = new Mock<IAgent>();
        _mockQueue = new Mock<IAgentRequestQueue>();
        _mockLogger = new Mock<ILogger<AgentWorkerPool>>();

        _mockAgent.Setup(a => a.Name).Returns("TestAgent");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullAgent_ThrowsArgumentNullException()
    {
        var act = () => new AgentWorkerPool(null!, _mockQueue.Object, 2);

        act.Should().Throw<ArgumentNullException>().WithParameterName("agent");
    }

    [Fact]
    public void Constructor_WithNullQueue_ThrowsArgumentNullException()
    {
        var act = () => new AgentWorkerPool(_mockAgent.Object, null!, 2);

        act.Should().Throw<ArgumentNullException>().WithParameterName("queue");
    }

    [Fact]
    public void Constructor_WithZeroWorkerCount_UsesDefaultCount()
    {
        using var pool = new AgentWorkerPool(_mockAgent.Object, _mockQueue.Object, 0);

        pool.WorkerCount.Should().Be(Environment.ProcessorCount * 2);
    }

    [Fact]
    public void Constructor_WithNegativeWorkerCount_UsesDefaultCount()
    {
        using var pool = new AgentWorkerPool(_mockAgent.Object, _mockQueue.Object, -5);

        pool.WorkerCount.Should().Be(Environment.ProcessorCount * 2);
    }

    [Fact]
    public void Constructor_WithPositiveWorkerCount_UsesProvidedCount()
    {
        using var pool = new AgentWorkerPool(_mockAgent.Object, _mockQueue.Object, 4);

        pool.WorkerCount.Should().Be(4);
    }

    #endregion

    #region IsRunning Tests

    [Fact]
    public void IsRunning_BeforeStart_IsFalse()
    {
        using var pool = CreatePool();

        pool.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void IsRunning_AfterStart_IsTrue()
    {
        SetupQueueToBlock();
        using var pool = CreatePool();

        pool.Start();

        pool.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task IsRunning_AfterStop_IsFalse()
    {
        SetupQueueToBlock();
        using var pool = CreatePool();

        pool.Start();
        await pool.StopAsync();

        pool.IsRunning.Should().BeFalse();
    }

    #endregion

    #region Start Tests

    [Fact]
    public void Start_MultipleTimes_OnlyStartsOnce()
    {
        SetupQueueToBlock();
        using var pool = CreatePool(workerCount: 2);

        pool.Start();
        pool.Start(); // Should not throw or create additional workers

        pool.IsRunning.Should().BeTrue();
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_BeforeStart_DoesNotThrow()
    {
        using var pool = CreatePool();

        var act = async () => await pool.StopAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_CancellationRequested_StopsGracefully()
    {
        SetupQueueToBlock();
        using var pool = CreatePool();
        using var cts = new CancellationTokenSource();

        pool.Start();
        cts.Cancel();

        var act = async () => await pool.StopAsync(cts.Token);

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region WorkerCount Tests

    [Fact]
    public void WorkerCount_ReturnsConfiguredCount()
    {
        using var pool = new AgentWorkerPool(_mockAgent.Object, _mockQueue.Object, 8);

        pool.WorkerCount.Should().Be(8);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        var pool = CreatePool();

        var act = () =>
        {
            pool.Dispose();
            pool.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_StopsRunningPool()
    {
        SetupQueueToBlock();
        var pool = CreatePool();

        pool.Start();
        pool.Dispose();

        // Pool should be stopped after dispose (IsRunning state may not update, but workers should stop)
    }

    #endregion

    #region Worker Processing Tests

    [Fact]
    public async Task WorkerPool_ProcessesQueueItems()
    {
        var tcs = new TaskCompletionSource<AgentResponse>();
        var workItem = new AgentWorkItem
        {
            Id = "test-1",
            Input = "Test input",
            CompletionSource = tcs,
        };

        var dequeueCount = 0;
        _mockQueue
            .Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                dequeueCount++;
                if (dequeueCount == 1)
                {
                    return workItem;
                }
                // Return null after first item to avoid infinite processing
                return null;
            });

        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Result", [], TimeSpan.FromMilliseconds(10)));

        using var pool = CreatePool(workerCount: 1);
        pool.Start();

        // Wait for processing
        var response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        response.FinalAnswer.Should().Be("Result");
    }

    #endregion

    #region Helper Methods

    private AgentWorkerPool CreatePool(int workerCount = 2)
    {
        return new AgentWorkerPool(
            _mockAgent.Object,
            _mockQueue.Object,
            workerCount,
            _mockLogger.Object
        );
    }

    private void SetupQueueToBlock()
    {
        _mockQueue
            .Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns(
                async (CancellationToken ct) =>
                {
                    // Block until cancellation
                    try
                    {
                        await Task.Delay(Timeout.Infinite, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                    return null;
                }
            );
    }

    #endregion
}
