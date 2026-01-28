namespace Dawning.Agents.Abstractions.Diagnostics;

/// <summary>
/// 诊断信息提供者接口
/// </summary>
public interface IDiagnosticsProvider
{
    /// <summary>
    /// 获取完整诊断信息
    /// </summary>
    Task<DiagnosticsInfo> GetDiagnosticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取内存信息
    /// </summary>
    MemoryInfo GetMemoryInfo();

    /// <summary>
    /// 获取 GC 信息
    /// </summary>
    GCInfo GetGCInfo();

    /// <summary>
    /// 获取线程池信息
    /// </summary>
    ThreadPoolInfo GetThreadPoolInfo();

    /// <summary>
    /// 获取进程信息
    /// </summary>
    ProcessInfo GetProcessInfo();

    /// <summary>
    /// 获取 Agent 运行时信息
    /// </summary>
    AgentRuntimeInfo GetAgentRuntimeInfo();
}

/// <summary>
/// 完整诊断信息
/// </summary>
public class DiagnosticsInfo
{
    /// <summary>
    /// 采集时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 内存信息
    /// </summary>
    public MemoryInfo Memory { get; set; } = new();

    /// <summary>
    /// GC 信息
    /// </summary>
    public GCInfo GC { get; set; } = new();

    /// <summary>
    /// 线程池信息
    /// </summary>
    public ThreadPoolInfo ThreadPool { get; set; } = new();

    /// <summary>
    /// 进程信息
    /// </summary>
    public ProcessInfo Process { get; set; } = new();

    /// <summary>
    /// Agent 运行时信息
    /// </summary>
    public AgentRuntimeInfo AgentRuntime { get; set; } = new();

    /// <summary>
    /// 环境信息
    /// </summary>
    public EnvironmentInfo Environment { get; set; } = new();
}

/// <summary>
/// 内存信息
/// </summary>
public class MemoryInfo
{
    /// <summary>
    /// 工作集大小（字节）
    /// </summary>
    public long WorkingSetBytes { get; set; }

    /// <summary>
    /// 工作集大小（MB）
    /// </summary>
    public double WorkingSetMB => WorkingSetBytes / (1024.0 * 1024.0);

    /// <summary>
    /// 私有内存（字节）
    /// </summary>
    public long PrivateMemoryBytes { get; set; }

    /// <summary>
    /// 私有内存（MB）
    /// </summary>
    public double PrivateMemoryMB => PrivateMemoryBytes / (1024.0 * 1024.0);

    /// <summary>
    /// GC 堆大小（字节）
    /// </summary>
    public long GCHeapSizeBytes { get; set; }

    /// <summary>
    /// GC 堆大小（MB）
    /// </summary>
    public double GCHeapSizeMB => GCHeapSizeBytes / (1024.0 * 1024.0);

    /// <summary>
    /// 托管内存（字节）
    /// </summary>
    public long ManagedMemoryBytes { get; set; }

    /// <summary>
    /// 托管内存（MB）
    /// </summary>
    public double ManagedMemoryMB => ManagedMemoryBytes / (1024.0 * 1024.0);

    /// <summary>
    /// 分配的内存总计（字节）
    /// </summary>
    public long TotalAllocatedBytes { get; set; }
}

/// <summary>
/// GC 信息
/// </summary>
public class GCInfo
{
    /// <summary>
    /// Gen0 回收次数
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Gen1 回收次数
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Gen2 回收次数
    /// </summary>
    public int Gen2Collections { get; set; }

    /// <summary>
    /// 总回收次数
    /// </summary>
    public int TotalCollections => Gen0Collections + Gen1Collections + Gen2Collections;

    /// <summary>
    /// GC 暂停时间百分比
    /// </summary>
    public double PauseTimePercentage { get; set; }

    /// <summary>
    /// 最后一次 GC 暂停时间（毫秒）
    /// </summary>
    public double LastPauseDurationMs { get; set; }

    /// <summary>
    /// 是否为服务器 GC
    /// </summary>
    public bool IsServerGC { get; set; }

    /// <summary>
    /// 是否为并发 GC
    /// </summary>
    public bool IsConcurrentGC { get; set; }

    /// <summary>
    /// GC 延迟模式
    /// </summary>
    public string LatencyMode { get; set; } = string.Empty;

    /// <summary>
    /// 内存压力
    /// </summary>
    public string MemoryLoad { get; set; } = string.Empty;
}

/// <summary>
/// 线程池信息
/// </summary>
public class ThreadPoolInfo
{
    /// <summary>
    /// 可用工作线程数
    /// </summary>
    public int AvailableWorkerThreads { get; set; }

    /// <summary>
    /// 可用 IO 线程数
    /// </summary>
    public int AvailableIOThreads { get; set; }

    /// <summary>
    /// 最小工作线程数
    /// </summary>
    public int MinWorkerThreads { get; set; }

    /// <summary>
    /// 最小 IO 线程数
    /// </summary>
    public int MinIOThreads { get; set; }

    /// <summary>
    /// 最大工作线程数
    /// </summary>
    public int MaxWorkerThreads { get; set; }

    /// <summary>
    /// 最大 IO 线程数
    /// </summary>
    public int MaxIOThreads { get; set; }

    /// <summary>
    /// 正在使用的工作线程数
    /// </summary>
    public int BusyWorkerThreads => MaxWorkerThreads - AvailableWorkerThreads;

    /// <summary>
    /// 正在使用的 IO 线程数
    /// </summary>
    public int BusyIOThreads => MaxIOThreads - AvailableIOThreads;

    /// <summary>
    /// 排队的工作项数
    /// </summary>
    public long PendingWorkItemCount { get; set; }

    /// <summary>
    /// 已完成的工作项数
    /// </summary>
    public long CompletedWorkItemCount { get; set; }

    /// <summary>
    /// 线程数
    /// </summary>
    public int ThreadCount { get; set; }
}

/// <summary>
/// 进程信息
/// </summary>
public class ProcessInfo
{
    /// <summary>
    /// 进程 ID
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// 进程名称
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// 启动时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 运行时长
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// CPU 使用时间
    /// </summary>
    public TimeSpan TotalProcessorTime { get; set; }

    /// <summary>
    /// 用户态 CPU 时间
    /// </summary>
    public TimeSpan UserProcessorTime { get; set; }

    /// <summary>
    /// 句柄数
    /// </summary>
    public int HandleCount { get; set; }
}

/// <summary>
/// Agent 运行时信息
/// </summary>
public class AgentRuntimeInfo
{
    /// <summary>
    /// 已注册的 Agent 数量
    /// </summary>
    public int RegisteredAgentCount { get; set; }

    /// <summary>
    /// 已注册的工具数量
    /// </summary>
    public int RegisteredToolCount { get; set; }

    /// <summary>
    /// 活跃的会话数量
    /// </summary>
    public int ActiveSessionCount { get; set; }

    /// <summary>
    /// 总请求数
    /// </summary>
    public long TotalRequestCount { get; set; }

    /// <summary>
    /// 当前正在处理的请求数
    /// </summary>
    public int CurrentRequestCount { get; set; }

    /// <summary>
    /// LLM 调用总数
    /// </summary>
    public long TotalLLMCalls { get; set; }

    /// <summary>
    /// 工具执行总数
    /// </summary>
    public long TotalToolExecutions { get; set; }
}

/// <summary>
/// 环境信息
/// </summary>
public class EnvironmentInfo
{
    /// <summary>
    /// 机器名称
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// 操作系统
    /// </summary>
    public string OSDescription { get; set; } = string.Empty;

    /// <summary>
    /// .NET 版本
    /// </summary>
    public string FrameworkDescription { get; set; } = string.Empty;

    /// <summary>
    /// 处理器架构
    /// </summary>
    public string ProcessorArchitecture { get; set; } = string.Empty;

    /// <summary>
    /// 处理器数量
    /// </summary>
    public int ProcessorCount { get; set; }

    /// <summary>
    /// 是否为 64 位进程
    /// </summary>
    public bool Is64BitProcess { get; set; }

    /// <summary>
    /// 当前目录
    /// </summary>
    public string CurrentDirectory { get; set; } = string.Empty;
}
