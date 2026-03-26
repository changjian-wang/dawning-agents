namespace Dawning.Agents.Abstractions.Diagnostics;

/// <summary>
/// Diagnostics information provider interface.
/// </summary>
public interface IDiagnosticsProvider
{
    /// <summary>
    /// Gets complete diagnostics information.
    /// </summary>
    Task<DiagnosticsInfo> GetDiagnosticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets memory information.
    /// </summary>
    MemoryInfo GetMemoryInfo();

    /// <summary>
    /// Gets GC information.
    /// </summary>
    GCInfo GetGCInfo();

    /// <summary>
    /// Gets thread pool information.
    /// </summary>
    ThreadPoolInfo GetThreadPoolInfo();

    /// <summary>
    /// Gets process information.
    /// </summary>
    ProcessInfo GetProcessInfo();

    /// <summary>
    /// Gets agent runtime information.
    /// </summary>
    AgentRuntimeInfo GetAgentRuntimeInfo();
}

/// <summary>
/// Complete diagnostics information.
/// </summary>
public class DiagnosticsInfo
{
    /// <summary>
    /// Collection timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Memory information.
    /// </summary>
    public MemoryInfo Memory { get; set; } = new();

    /// <summary>
    /// GC information.
    /// </summary>
    public GCInfo GC { get; set; } = new();

    /// <summary>
    /// Thread pool information.
    /// </summary>
    public ThreadPoolInfo ThreadPool { get; set; } = new();

    /// <summary>
    /// Process information.
    /// </summary>
    public ProcessInfo Process { get; set; } = new();

    /// <summary>
    /// Agent runtime information.
    /// </summary>
    public AgentRuntimeInfo AgentRuntime { get; set; } = new();

    /// <summary>
    /// Environment information.
    /// </summary>
    public EnvironmentInfo Environment { get; set; } = new();
}

/// <summary>
/// Memory information.
/// </summary>
public class MemoryInfo
{
    /// <summary>
    /// Working set size (bytes).
    /// </summary>
    public long WorkingSetBytes { get; set; }

    /// <summary>
    /// Working set size (MB).
    /// </summary>
    public double WorkingSetMB => WorkingSetBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Private memory (bytes).
    /// </summary>
    public long PrivateMemoryBytes { get; set; }

    /// <summary>
    /// Private memory (MB).
    /// </summary>
    public double PrivateMemoryMB => PrivateMemoryBytes / (1024.0 * 1024.0);

    /// <summary>
    /// GC heap size (bytes).
    /// </summary>
    public long GCHeapSizeBytes { get; set; }

    /// <summary>
    /// GC heap size (MB).
    /// </summary>
    public double GCHeapSizeMB => GCHeapSizeBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Managed memory (bytes).
    /// </summary>
    public long ManagedMemoryBytes { get; set; }

    /// <summary>
    /// Managed memory (MB).
    /// </summary>
    public double ManagedMemoryMB => ManagedMemoryBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Total allocated memory (bytes).
    /// </summary>
    public long TotalAllocatedBytes { get; set; }
}

/// <summary>
/// GC information.
/// </summary>
public class GCInfo
{
    /// <summary>
    /// Gen0 collection count.
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Gen1 collection count.
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Gen2 collection count.
    /// </summary>
    public int Gen2Collections { get; set; }

    /// <summary>
    /// Total collection count.
    /// </summary>
    public int TotalCollections => Gen0Collections + Gen1Collections + Gen2Collections;

    /// <summary>
    /// GC pause time percentage.
    /// </summary>
    public double PauseTimePercentage { get; set; }

    /// <summary>
    /// Last GC pause duration (milliseconds).
    /// </summary>
    public double LastPauseDurationMs { get; set; }

    /// <summary>
    /// Whether server GC is enabled.
    /// </summary>
    public bool IsServerGC { get; set; }

    /// <summary>
    /// Whether concurrent GC is enabled.
    /// </summary>
    public bool IsConcurrentGC { get; set; }

    /// <summary>
    /// GC latency mode.
    /// </summary>
    public string LatencyMode { get; set; } = string.Empty;

    /// <summary>
    /// Memory load.
    /// </summary>
    public string MemoryLoad { get; set; } = string.Empty;
}

/// <summary>
/// Thread pool information.
/// </summary>
public class ThreadPoolInfo
{
    /// <summary>
    /// Available worker thread count.
    /// </summary>
    public int AvailableWorkerThreads { get; set; }

    /// <summary>
    /// Available I/O thread count.
    /// </summary>
    public int AvailableIOThreads { get; set; }

    /// <summary>
    /// Minimum worker thread count.
    /// </summary>
    public int MinWorkerThreads { get; set; }

    /// <summary>
    /// Minimum I/O thread count.
    /// </summary>
    public int MinIOThreads { get; set; }

    /// <summary>
    /// Maximum worker thread count.
    /// </summary>
    public int MaxWorkerThreads { get; set; }

    /// <summary>
    /// Maximum I/O thread count.
    /// </summary>
    public int MaxIOThreads { get; set; }

    /// <summary>
    /// Busy worker thread count.
    /// </summary>
    public int BusyWorkerThreads => MaxWorkerThreads - AvailableWorkerThreads;

    /// <summary>
    /// Busy I/O thread count.
    /// </summary>
    public int BusyIOThreads => MaxIOThreads - AvailableIOThreads;

    /// <summary>
    /// Pending work item count.
    /// </summary>
    public long PendingWorkItemCount { get; set; }

    /// <summary>
    /// Completed work item count.
    /// </summary>
    public long CompletedWorkItemCount { get; set; }

    /// <summary>
    /// Thread count.
    /// </summary>
    public int ThreadCount { get; set; }
}

/// <summary>
/// Process information.
/// </summary>
public class ProcessInfo
{
    /// <summary>
    /// Process ID.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Process name.
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Start time.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Uptime duration.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Total CPU time.
    /// </summary>
    public TimeSpan TotalProcessorTime { get; set; }

    /// <summary>
    /// User-mode CPU time.
    /// </summary>
    public TimeSpan UserProcessorTime { get; set; }

    /// <summary>
    /// Handle count.
    /// </summary>
    public int HandleCount { get; set; }
}

/// <summary>
/// Agent runtime information.
/// </summary>
public class AgentRuntimeInfo
{
    /// <summary>
    /// Registered agent count.
    /// </summary>
    public int RegisteredAgentCount { get; set; }

    /// <summary>
    /// Registered tool count.
    /// </summary>
    public int RegisteredToolCount { get; set; }

    /// <summary>
    /// Active session count.
    /// </summary>
    public int ActiveSessionCount { get; set; }

    /// <summary>
    /// Total request count.
    /// </summary>
    public long TotalRequestCount { get; set; }

    /// <summary>
    /// Current in-flight request count.
    /// </summary>
    public int CurrentRequestCount { get; set; }

    /// <summary>
    /// Total LLM call count.
    /// </summary>
    public long TotalLLMCalls { get; set; }

    /// <summary>
    /// Total tool execution count.
    /// </summary>
    public long TotalToolExecutions { get; set; }
}

/// <summary>
/// Environment information.
/// </summary>
public class EnvironmentInfo
{
    /// <summary>
    /// Machine name.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Operating system.
    /// </summary>
    public string OSDescription { get; set; } = string.Empty;

    /// <summary>
    /// .NET version.
    /// </summary>
    public string FrameworkDescription { get; set; } = string.Empty;

    /// <summary>
    /// Processor architecture.
    /// </summary>
    public string ProcessorArchitecture { get; set; } = string.Empty;

    /// <summary>
    /// Processor count.
    /// </summary>
    public int ProcessorCount { get; set; }

    /// <summary>
    /// Whether the process is 64-bit.
    /// </summary>
    public bool Is64BitProcess { get; set; }

    /// <summary>
    /// Current directory.
    /// </summary>
    public string CurrentDirectory { get; set; } = string.Empty;
}
