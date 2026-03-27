using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using Dawning.Agents.Abstractions.Diagnostics;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Diagnostics;

/// <summary>
/// Diagnostics information provider implementation.
/// </summary>
public sealed class DiagnosticsProvider : IDiagnosticsProvider
{
    private readonly IToolReader? _toolRegistry;

    // Runtime statistics (simple counters)
    private static long _totalRequestCount;
    private static int _currentRequestCount;
    private static long _totalLLMCalls;
    private static long _totalToolExecutions;
    private static int _activeSessionCount;

    public DiagnosticsProvider(
        IToolReader? toolRegistry = null,
        ILogger<DiagnosticsProvider>? logger = null
    )
    {
        _toolRegistry = toolRegistry;
    }

    /// <inheritdoc />
    public Task<DiagnosticsInfo> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var info = new DiagnosticsInfo
        {
            Timestamp = DateTimeOffset.UtcNow,
            Memory = GetMemoryInfo(),
            GC = GetGCInfo(),
            ThreadPool = GetThreadPoolInfo(),
            Process = GetProcessInfo(),
            AgentRuntime = GetAgentRuntimeInfo(),
            Environment = GetEnvironmentInfo(),
        };

        return Task.FromResult(info);
    }

    /// <inheritdoc />
    public MemoryInfo GetMemoryInfo()
    {
        using var process = Process.GetCurrentProcess();
        var gcMemoryInfo = GC.GetGCMemoryInfo();

        return new MemoryInfo
        {
            WorkingSetBytes = process.WorkingSet64,
            PrivateMemoryBytes = process.PrivateMemorySize64,
            GCHeapSizeBytes = gcMemoryInfo.HeapSizeBytes,
            ManagedMemoryBytes = GC.GetTotalMemory(forceFullCollection: false),
            TotalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false),
        };
    }

    /// <inheritdoc />
    public GCInfo GetGCInfo()
    {
        var gcMemoryInfo = GC.GetGCMemoryInfo();

        return new GCInfo
        {
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            PauseTimePercentage = gcMemoryInfo.PauseTimePercentage,
            LastPauseDurationMs =
                gcMemoryInfo.PauseDurations.Length > 0
                    ? gcMemoryInfo.PauseDurations[^1].TotalMilliseconds
                    : 0,
            IsServerGC = GCSettings.IsServerGC,
            IsConcurrentGC = gcMemoryInfo.Concurrent,
            LatencyMode = GCSettings.LatencyMode.ToString(),
            MemoryLoad = GetMemoryLoadDescription(gcMemoryInfo.MemoryLoadBytes),
        };
    }

    /// <inheritdoc />
    public ThreadPoolInfo GetThreadPoolInfo()
    {
        ThreadPool.GetAvailableThreads(out var availableWorker, out var availableIO);
        ThreadPool.GetMinThreads(out var minWorker, out var minIO);
        ThreadPool.GetMaxThreads(out var maxWorker, out var maxIO);

        return new ThreadPoolInfo
        {
            AvailableWorkerThreads = availableWorker,
            AvailableIOThreads = availableIO,
            MinWorkerThreads = minWorker,
            MinIOThreads = minIO,
            MaxWorkerThreads = maxWorker,
            MaxIOThreads = maxIO,
            PendingWorkItemCount = ThreadPool.PendingWorkItemCount,
            CompletedWorkItemCount = ThreadPool.CompletedWorkItemCount,
            ThreadCount = ThreadPool.ThreadCount,
        };
    }

    /// <inheritdoc />
    public ProcessInfo GetProcessInfo()
    {
        using var process = Process.GetCurrentProcess();

        return new ProcessInfo
        {
            ProcessId = process.Id,
            ProcessName = process.ProcessName,
            StartTime = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero),
            Uptime =
                DateTimeOffset.UtcNow
                - new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero),
            TotalProcessorTime = process.TotalProcessorTime,
            UserProcessorTime = process.UserProcessorTime,
            HandleCount = process.HandleCount,
        };
    }

    /// <inheritdoc />
    public AgentRuntimeInfo GetAgentRuntimeInfo()
    {
        return new AgentRuntimeInfo
        {
            RegisteredAgentCount = 0, // TODO: Get from agent registry
            RegisteredToolCount = _toolRegistry?.GetAllTools().Count ?? 0,
            ActiveSessionCount = _activeSessionCount,
            TotalRequestCount = _totalRequestCount,
            CurrentRequestCount = _currentRequestCount,
            TotalLLMCalls = _totalLLMCalls,
            TotalToolExecutions = _totalToolExecutions,
        };
    }

    /// <summary>
    /// Gets environment information.
    /// </summary>
    private static EnvironmentInfo GetEnvironmentInfo()
    {
        return new EnvironmentInfo
        {
            MachineName = Environment.MachineName,
            OSDescription = RuntimeInformation.OSDescription,
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            ProcessorArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            Is64BitProcess = Environment.Is64BitProcess,
            CurrentDirectory = Environment.CurrentDirectory,
        };
    }

    /// <summary>
    /// Gets a memory load description.
    /// </summary>
    private static string GetMemoryLoadDescription(long memoryLoadBytes)
    {
        var loadMB = memoryLoadBytes / (1024.0 * 1024.0);
        return loadMB switch
        {
            < 100 => "Low",
            < 500 => "Normal",
            < 1000 => "Moderate",
            < 2000 => "High",
            _ => "Critical",
        };
    }

    #region Static statistics methods

    /// <summary>
    /// Records a request start.
    /// </summary>
    public static void RecordRequestStart()
    {
        Interlocked.Increment(ref _totalRequestCount);
        Interlocked.Increment(ref _currentRequestCount);
    }

    /// <summary>
    /// Records a request end.
    /// </summary>
    public static void RecordRequestEnd()
    {
        Interlocked.Decrement(ref _currentRequestCount);
    }

    /// <summary>
    /// Records an LLM call.
    /// </summary>
    public static void RecordLLMCall()
    {
        Interlocked.Increment(ref _totalLLMCalls);
    }

    /// <summary>
    /// Records a tool execution.
    /// </summary>
    public static void RecordToolExecution()
    {
        Interlocked.Increment(ref _totalToolExecutions);
    }

    /// <summary>
    /// Records a session start.
    /// </summary>
    public static void RecordSessionStart()
    {
        Interlocked.Increment(ref _activeSessionCount);
    }

    /// <summary>
    /// Records a session end.
    /// </summary>
    public static void RecordSessionEnd()
    {
        Interlocked.Decrement(ref _activeSessionCount);
    }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public static void ResetStatistics()
    {
        Interlocked.Exchange(ref _totalRequestCount, 0);
        Interlocked.Exchange(ref _currentRequestCount, 0);
        Interlocked.Exchange(ref _totalLLMCalls, 0);
        Interlocked.Exchange(ref _totalToolExecutions, 0);
        Interlocked.Exchange(ref _activeSessionCount, 0);
    }

    #endregion
}
