namespace Dawning.Agents.Abstractions.Orchestration;

/// <summary>
/// Orchestration execution context (thread-safe).
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
    /// Session ID.
    /// </summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Original user input.
    /// </summary>
    public required string UserInput { get; init; }

    /// <summary>
    /// Current input (may be modified by a previous Agent).
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
    /// Executed Agent records (read-only snapshot).
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
    /// Custom metadata that can be shared between Agents (read-only snapshot).
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
    /// Adds an execution record.
    /// </summary>
    public void AddExecutionRecord(AgentExecutionRecord record)
    {
        lock (_lock)
        {
            _executionHistory.Add(record);
        }
    }

    /// <summary>
    /// Sets metadata.
    /// </summary>
    public void SetMetadata(string key, object value)
    {
        lock (_lock)
        {
            _metadata[key] = value;
        }
    }

    /// <summary>
    /// Whether execution should stop (used for conditional routing).
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
    /// Stop reason.
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
