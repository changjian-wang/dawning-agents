using Dawning.Agents.Abstractions.Diagnostics;
using Dawning.Agents.Core.Diagnostics;
using FluentAssertions;

namespace Dawning.Agents.Tests.Diagnostics;

public class DiagnosticsProviderTests
{
    [Fact]
    public async Task GetDiagnosticsAsync_ReturnsCompleteInfo()
    {
        // Arrange
        var provider = new DiagnosticsProvider();

        // Act
        var info = await provider.GetDiagnosticsAsync();

        // Assert
        info.Should().NotBeNull();
        info.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        info.Memory.Should().NotBeNull();
        info.GC.Should().NotBeNull();
        info.ThreadPool.Should().NotBeNull();
        info.Process.Should().NotBeNull();
        info.Environment.Should().NotBeNull();
    }

    [Fact]
    public void GetMemoryInfo_ReturnsValidData()
    {
        // Arrange
        var provider = new DiagnosticsProvider();

        // Act
        var memoryInfo = provider.GetMemoryInfo();

        // Assert
        memoryInfo.WorkingSetBytes.Should().BeGreaterThan(0);
        memoryInfo.GCHeapSizeBytes.Should().BeGreaterThan(0);
        memoryInfo.ManagedMemoryBytes.Should().BeGreaterThan(0);
        memoryInfo.WorkingSetMB.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetGCInfo_ReturnsValidData()
    {
        // Arrange
        var provider = new DiagnosticsProvider();

        // Act
        var gcInfo = provider.GetGCInfo();

        // Assert
        gcInfo.Gen0Collections.Should().BeGreaterThanOrEqualTo(0);
        gcInfo.Gen1Collections.Should().BeGreaterThanOrEqualTo(0);
        gcInfo.Gen2Collections.Should().BeGreaterThanOrEqualTo(0);
        gcInfo.LatencyMode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetThreadPoolInfo_ReturnsValidData()
    {
        // Arrange
        var provider = new DiagnosticsProvider();

        // Act
        var threadPoolInfo = provider.GetThreadPoolInfo();

        // Assert
        threadPoolInfo.MaxWorkerThreads.Should().BeGreaterThan(0);
        threadPoolInfo.MaxIOThreads.Should().BeGreaterThan(0);
        threadPoolInfo.AvailableWorkerThreads.Should().BeGreaterThan(0);
        threadPoolInfo.ThreadCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetProcessInfo_ReturnsValidData()
    {
        // Arrange
        var provider = new DiagnosticsProvider();

        // Act
        var processInfo = provider.GetProcessInfo();

        // Assert
        processInfo.ProcessId.Should().BeGreaterThan(0);
        processInfo.ProcessName.Should().NotBeNullOrEmpty();
        processInfo.StartTime.Should().BeBefore(DateTime.UtcNow);
        processInfo.Uptime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void StaticStatistics_TrackCorrectly()
    {
        // Arrange
        DiagnosticsProvider.ResetStatistics();
        var provider = new DiagnosticsProvider();

        // Act
        DiagnosticsProvider.RecordRequestStart();
        DiagnosticsProvider.RecordRequestStart();
        DiagnosticsProvider.RecordLLMCall();
        DiagnosticsProvider.RecordToolExecution();
        DiagnosticsProvider.RecordToolExecution();
        DiagnosticsProvider.RecordSessionStart();
        DiagnosticsProvider.RecordRequestEnd();

        var runtimeInfo = provider.GetAgentRuntimeInfo();

        // Assert
        runtimeInfo.TotalRequestCount.Should().Be(2);
        runtimeInfo.CurrentRequestCount.Should().Be(1);
        runtimeInfo.TotalLLMCalls.Should().Be(1);
        runtimeInfo.TotalToolExecutions.Should().Be(2);
        runtimeInfo.ActiveSessionCount.Should().Be(1);

        // Cleanup
        DiagnosticsProvider.ResetStatistics();
    }
}

public class PerformanceProfilerTests
{
    [Fact]
    public void RecordOperation_TracksCorrectly()
    {
        // Arrange
        var profiler = new PerformanceProfiler();

        // Act
        profiler.RecordOperation("TestOperation", TimeSpan.FromMilliseconds(100), "Test");

        var stats = profiler.GetStatistics();

        // Assert
        stats.Should().ContainKey("TestOperation");
        stats["TestOperation"].TotalCount.Should().Be(1);
        stats["TestOperation"].AverageDuration.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void StartOperation_MeasuresDuration()
    {
        // Arrange
        var profiler = new PerformanceProfiler();

        // Act
        using (profiler.StartOperation("TimedOperation", "Test"))
        {
            Thread.Sleep(50); // 模拟工作
        }

        var stats = profiler.GetStatistics();

        // Assert
        stats.Should().ContainKey("TimedOperation");
        stats["TimedOperation"].TotalCount.Should().Be(1);
        stats["TimedOperation"].AverageDuration.TotalMilliseconds.Should().BeGreaterThan(40);
    }

    [Fact]
    public void GetSlowOperations_FiltersByThreshold()
    {
        // Arrange
        var profiler = new PerformanceProfiler();
        profiler.RecordOperation("FastOp", TimeSpan.FromMilliseconds(10));
        profiler.RecordOperation("SlowOp", TimeSpan.FromMilliseconds(500));
        profiler.RecordOperation("VerySlowOp", TimeSpan.FromMilliseconds(2000));

        // Act
        var slowOps = profiler.GetSlowOperations(TimeSpan.FromMilliseconds(100));

        // Assert
        slowOps.Should().HaveCount(2);
        slowOps[0].OperationName.Should().Be("VerySlowOp");
        slowOps[1].OperationName.Should().Be("SlowOp");
    }

    [Fact]
    public void GetStatistics_FiltersByCategory()
    {
        // Arrange
        var profiler = new PerformanceProfiler();
        profiler.RecordOperation("LLM:gpt-4", TimeSpan.FromMilliseconds(100), OperationCategories.LLM);
        profiler.RecordOperation("Tool:search", TimeSpan.FromMilliseconds(50), OperationCategories.Tool);
        profiler.RecordOperation("LLM:claude", TimeSpan.FromMilliseconds(150), OperationCategories.LLM);

        // Act
        var llmStats = profiler.GetStatistics(OperationCategories.LLM);
        var allStats = profiler.GetStatistics();

        // Assert
        llmStats.Should().HaveCount(2);
        allStats.Should().HaveCount(3);
    }

    [Fact]
    public void Statistics_TrackMinMaxCorrectly()
    {
        // Arrange
        var profiler = new PerformanceProfiler();

        // Act
        profiler.RecordOperation("Op", TimeSpan.FromMilliseconds(100));
        profiler.RecordOperation("Op", TimeSpan.FromMilliseconds(50));
        profiler.RecordOperation("Op", TimeSpan.FromMilliseconds(200));

        var stats = profiler.GetStatistics()["Op"];

        // Assert
        stats.TotalCount.Should().Be(3);
        stats.MinDuration.Should().Be(TimeSpan.FromMilliseconds(50));
        stats.MaxDuration.Should().Be(TimeSpan.FromMilliseconds(200));
        stats.AverageDuration.TotalMilliseconds.Should().BeApproximately(116.67, 1);
    }

    [Fact]
    public void Clear_RemovesAllData()
    {
        // Arrange
        var profiler = new PerformanceProfiler();
        profiler.RecordOperation("Op1", TimeSpan.FromMilliseconds(100));
        profiler.RecordOperation("Op2", TimeSpan.FromMilliseconds(200));

        // Act
        profiler.Clear();

        // Assert
        profiler.GetStatistics().Should().BeEmpty();
        profiler.GetSlowOperations(TimeSpan.Zero).Should().BeEmpty();
    }

    [Fact]
    public void ProfileLLMCall_CreatesCorrectOperationName()
    {
        // Arrange
        var profiler = new PerformanceProfiler();

        // Act
        using (profiler.ProfileLLMCall("gpt-4", "OpenAI"))
        {
            // 模拟工作
        }

        var stats = profiler.GetStatistics(OperationCategories.LLM);

        // Assert
        stats.Should().ContainKey("LLM:OpenAI:gpt-4");
    }

    [Fact]
    public void ProfileToolExecution_CreatesCorrectOperationName()
    {
        // Arrange
        var profiler = new PerformanceProfiler();

        // Act
        using (profiler.ProfileToolExecution("web_search"))
        {
            // 模拟工作
        }

        var stats = profiler.GetStatistics(OperationCategories.Tool);

        // Assert
        stats.Should().ContainKey("Tool:web_search");
    }
}
