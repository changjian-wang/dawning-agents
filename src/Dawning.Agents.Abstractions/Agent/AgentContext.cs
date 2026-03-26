namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent execution context containing all state information for the current session (thread-safe).
/// </summary>
public class AgentContext
{
    private readonly List<AgentStep> _steps = [];
    private readonly Dictionary<string, object> _metadata = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Session ID.
    /// </summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Original user input.
    /// </summary>
    public required string UserInput { get; init; }

    /// <summary>
    /// Execution step history (read-only snapshot).
    /// </summary>
    public IReadOnlyList<AgentStep> Steps
    {
        get
        {
            lock (_lock)
            {
                return _steps.ToList();
            }
        }
    }

    /// <summary>
    /// Maximum number of execution steps to prevent infinite loops.
    /// </summary>
    public int MaxSteps { get; init; } = 10;

    /// <summary>
    /// Custom metadata (read-only snapshot).
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
    /// Adds an execution step.
    /// </summary>
    /// <param name="step">The step to add.</param>
    public void AddStep(AgentStep step)
    {
        lock (_lock)
        {
            _steps.Add(step);
        }
    }

    /// <summary>
    /// Sets a metadata entry.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public void SetMetadata(string key, object value)
    {
        lock (_lock)
        {
            _metadata[key] = value;
        }
    }
}
