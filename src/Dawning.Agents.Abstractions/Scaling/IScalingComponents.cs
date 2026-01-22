namespace Dawning.Agents.Abstractions.Scaling;

using Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent 工作项
/// </summary>
public record AgentWorkItem
{
    /// <summary>
    /// 工作项 ID
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 输入内容
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// Agent 上下文
    /// </summary>
    public AgentContext? Context { get; init; }

    /// <summary>
    /// 完成回调
    /// </summary>
    public required TaskCompletionSource<AgentResponse> CompletionSource { get; init; }

    /// <summary>
    /// 入队时间
    /// </summary>
    public DateTime EnqueuedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 优先级
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Agent 实例信息
/// </summary>
public record AgentInstance
{
    /// <summary>
    /// 实例 ID
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Agent 实例
    /// </summary>
    public required IAgent Agent { get; init; }

    /// <summary>
    /// 端点地址
    /// </summary>
    public string Endpoint { get; init; } = "";

    /// <summary>
    /// 是否健康
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// 活跃请求数
    /// </summary>
    public int ActiveRequests { get; set; }

    /// <summary>
    /// 最后健康检查时间
    /// </summary>
    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 标签
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = [];
}

/// <summary>
/// 请求队列接口
/// </summary>
public interface IAgentRequestQueue
{
    /// <summary>
    /// 入队工作项
    /// </summary>
    ValueTask EnqueueAsync(AgentWorkItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// 出队工作项
    /// </summary>
    ValueTask<AgentWorkItem?> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 当前队列长度
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 是否可以接受更多项
    /// </summary>
    bool CanWrite { get; }
}

/// <summary>
/// 工作池接口
/// </summary>
public interface IAgentWorkerPool : IDisposable
{
    /// <summary>
    /// 启动工作池
    /// </summary>
    void Start();

    /// <summary>
    /// 停止工作池
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 工作线程数
    /// </summary>
    int WorkerCount { get; }

    /// <summary>
    /// 是否正在运行
    /// </summary>
    bool IsRunning { get; }
}

/// <summary>
/// 负载均衡器接口
/// </summary>
public interface IAgentLoadBalancer
{
    /// <summary>
    /// 注册实例
    /// </summary>
    void RegisterInstance(AgentInstance instance);

    /// <summary>
    /// 注销实例
    /// </summary>
    void UnregisterInstance(string instanceId);

    /// <summary>
    /// 获取下一个实例（轮询）
    /// </summary>
    AgentInstance? GetNextInstance();

    /// <summary>
    /// 获取负载最小的实例
    /// </summary>
    AgentInstance? GetLeastLoadedInstance();

    /// <summary>
    /// 获取所有实例
    /// </summary>
    IReadOnlyList<AgentInstance> GetAllInstances();

    /// <summary>
    /// 获取健康实例数
    /// </summary>
    int HealthyInstanceCount { get; }
}

/// <summary>
/// 熔断器接口
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>
    /// 当前状态
    /// </summary>
    CircuitState State { get; }

    /// <summary>
    /// 带熔断保护执行
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// 带熔断保护执行（无返回值）
    /// </summary>
    Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置熔断器
    /// </summary>
    void Reset();

    /// <summary>
    /// 连续失败次数
    /// </summary>
    int FailureCount { get; }
}

/// <summary>
/// 自动扩展器接口
/// </summary>
public interface IAgentAutoScaler
{
    /// <summary>
    /// 评估并应用扩展决策
    /// </summary>
    Task<ScalingDecision> EvaluateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 当前实例数
    /// </summary>
    int CurrentInstances { get; }

    /// <summary>
    /// 上次扩容时间
    /// </summary>
    DateTime? LastScaleUpTime { get; }

    /// <summary>
    /// 上次缩容时间
    /// </summary>
    DateTime? LastScaleDownTime { get; }
}
