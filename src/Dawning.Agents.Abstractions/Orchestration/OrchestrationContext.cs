namespace Dawning.Agents.Abstractions.Orchestration;

/// <summary>
/// 编排执行上下文（线程安全）
/// </summary>
public class OrchestrationContext
{
    private readonly List<AgentExecutionRecord> _executionHistory = [];
    private readonly Dictionary<string, object> _metadata = [];
    private readonly Lock _lock = new();
    private string _currentInput = string.Empty;
    private bool _shouldStop;
    private string? _stopReason;

    /// <summary>
    /// 会话 ID
    /// </summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 原始用户输入
    /// </summary>
    public required string UserInput { get; init; }

    /// <summary>
    /// 当前输入（可能被前一个 Agent 修改）
    /// </summary>
    public string CurrentInput
    {
        get
        {
            lock (_lock)
            {
                return _currentInput;
            }
        }
        set
        {
            lock (_lock)
            {
                _currentInput = value ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// 已执行的 Agent 记录（只读快照）
    /// </summary>
    public IReadOnlyList<AgentExecutionRecord> ExecutionHistory
    {
        get
        {
            lock (_lock)
            {
                return _executionHistory.ToList();
            }
        }
    }

    /// <summary>
    /// 自定义元数据，可在 Agent 之间传递（只读快照）
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, object>(_metadata);
            }
        }
    }

    /// <summary>
    /// 添加执行记录
    /// </summary>
    public void AddExecutionRecord(AgentExecutionRecord record)
    {
        lock (_lock)
        {
            _executionHistory.Add(record);
        }
    }

    /// <summary>
    /// 设置元数据
    /// </summary>
    public void SetMetadata(string key, object value)
    {
        lock (_lock)
        {
            _metadata[key] = value;
        }
    }

    /// <summary>
    /// 是否应该停止执行（用于条件路由）
    /// </summary>
    public bool ShouldStop
    {
        get
        {
            lock (_lock)
            {
                return _shouldStop;
            }
        }
        set
        {
            lock (_lock)
            {
                _shouldStop = value;
            }
        }
    }

    /// <summary>
    /// 停止原因
    /// </summary>
    public string? StopReason
    {
        get
        {
            lock (_lock)
            {
                return _stopReason;
            }
        }
        set
        {
            lock (_lock)
            {
                _stopReason = value;
            }
        }
    }
}
