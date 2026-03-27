using Dawning.Agents.Abstractions.Workflow;

namespace Dawning.Agents.Core.Workflow;

/// <summary>
/// Fluent workflow builder.
/// </summary>
public sealed class WorkflowBuilder
{
    private readonly string _id;
    private string _name;
    private string? _description;
    private string _version = "1.0.0";
    private readonly List<WorkflowNodeDefinition> _nodes = [];
    private readonly List<WorkflowEdgeDefinition> _edges = [];
    private string? _startNodeId;
    private readonly Dictionary<string, object?> _metadata = new();

    private WorkflowBuilder(string id, string name)
    {
        _id = id;
        _name = name;
    }

    /// <summary>
    /// Creates a new workflow builder.
    /// </summary>
    public static WorkflowBuilder Create(string id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new WorkflowBuilder(id, name);
    }

    /// <summary>
    /// Sets the workflow description.
    /// </summary>
    public WorkflowBuilder WithDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the workflow version.
    /// </summary>
    public WorkflowBuilder WithVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        _version = version;
        return this;
    }

    /// <summary>
    /// Sets the start node.
    /// </summary>
    public WorkflowBuilder StartWith(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        _startNodeId = nodeId;
        return this;
    }

    /// <summary>
    /// Adds an agent node.
    /// </summary>
    public WorkflowBuilder AddAgentNode(
        string id,
        string name,
        string agentName,
        string? inputTemplate = null,
        int maxRetries = 0,
        int? timeoutMs = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        var config = new Dictionary<string, object?>
        {
            ["agentName"] = agentName,
            ["inputTemplate"] = inputTemplate,
            ["maxRetries"] = maxRetries,
            ["timeoutMs"] = timeoutMs,
        };

        _nodes.Add(
            new WorkflowNodeDefinition
            {
                Id = id,
                Name = name,
                Type = WorkflowNodeType.Agent,
                Config = config,
            }
        );
        return this;
    }

    /// <summary>
    /// Adds a tool node.
    /// </summary>
    public WorkflowBuilder AddToolNode(
        string id,
        string name,
        string toolName,
        string? inputTemplate = null,
        int maxRetries = 0
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        var config = new Dictionary<string, object?>
        {
            ["toolName"] = toolName,
            ["inputTemplate"] = inputTemplate,
            ["maxRetries"] = maxRetries,
        };

        _nodes.Add(
            new WorkflowNodeDefinition
            {
                Id = id,
                Name = name,
                Type = WorkflowNodeType.Tool,
                Config = config,
            }
        );
        return this;
    }

    /// <summary>
    /// Adds a condition node.
    /// </summary>
    public WorkflowBuilder AddConditionNode(
        string id,
        string name,
        Action<ConditionNodeBuilder> configure
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ConditionNodeBuilder();
        configure(builder);
        var config = builder.Build();

        _nodes.Add(
            new WorkflowNodeDefinition
            {
                Id = id,
                Name = name,
                Type = WorkflowNodeType.Condition,
                Config = config,
            }
        );
        return this;
    }

    /// <summary>
    /// Adds a parallel node.
    /// </summary>
    public WorkflowBuilder AddParallelNode(
        string id,
        string name,
        Action<ParallelNodeBuilder> configure
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ParallelNodeBuilder();
        configure(builder);
        var config = builder.Build();

        _nodes.Add(
            new WorkflowNodeDefinition
            {
                Id = id,
                Name = name,
                Type = WorkflowNodeType.Parallel,
                Config = config,
            }
        );
        return this;
    }

    /// <summary>
    /// Adds a loop node.
    /// </summary>
    public WorkflowBuilder AddLoopNode(
        string id,
        string name,
        string bodyStartNodeId,
        int maxIterations = 10,
        LoopType loopType = LoopType.Count,
        string? loopCondition = null
    )
    {
        var config = new Dictionary<string, object?>
        {
            ["loopType"] = loopType.ToString(),
            ["maxIterations"] = maxIterations,
            ["loopCondition"] = loopCondition,
            ["bodyStartNodeId"] = bodyStartNodeId,
        };

        _nodes.Add(
            new WorkflowNodeDefinition
            {
                Id = id,
                Name = name,
                Type = WorkflowNodeType.Loop,
                Config = config,
            }
        );
        return this;
    }

    /// <summary>
    /// Adds a delay node.
    /// </summary>
    public WorkflowBuilder AddDelayNode(string id, string name, int delayMs)
    {
        var config = new Dictionary<string, object?> { ["delayMs"] = delayMs };

        _nodes.Add(
            new WorkflowNodeDefinition
            {
                Id = id,
                Name = name,
                Type = WorkflowNodeType.Delay,
                Config = config,
            }
        );
        return this;
    }

    /// <summary>
    /// Adds a start node.
    /// </summary>
    public WorkflowBuilder AddStartNode(string id = "start")
    {
        _nodes.Add(
            new WorkflowNodeDefinition
            {
                Id = id,
                Name = "Start",
                Type = WorkflowNodeType.Start,
            }
        );
        _startNodeId ??= id;
        return this;
    }

    /// <summary>
    /// Adds an end node.
    /// </summary>
    public WorkflowBuilder AddEndNode(string id = "end")
    {
        _nodes.Add(
            new WorkflowNodeDefinition
            {
                Id = id,
                Name = "End",
                Type = WorkflowNodeType.End,
            }
        );
        return this;
    }

    /// <summary>
    /// Adds a human approval node.
    /// </summary>
    public WorkflowBuilder AddHumanApprovalNode(
        string id,
        string name,
        string approvedNodeId,
        string? rejectedNodeId = null,
        string? message = null,
        int? timeoutMs = null
    )
    {
        var config = new Dictionary<string, object?>
        {
            ["message"] = message,
            ["timeoutMs"] = timeoutMs,
            ["approvedNodeId"] = approvedNodeId,
            ["rejectedNodeId"] = rejectedNodeId,
        };

        _nodes.Add(
            new WorkflowNodeDefinition
            {
                Id = id,
                Name = name,
                Type = WorkflowNodeType.HumanApproval,
                Config = config,
            }
        );
        return this;
    }

    /// <summary>
    /// Connects two nodes with an edge.
    /// </summary>
    public WorkflowBuilder Connect(string fromNodeId, string toNodeId, string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toNodeId);

        _edges.Add(
            new WorkflowEdgeDefinition
            {
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                Label = label,
            }
        );
        return this;
    }

    /// <summary>
    /// Adds metadata.
    /// </summary>
    public WorkflowBuilder WithMetadata(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Builds the workflow definition.
    /// </summary>
    public WorkflowDefinition Build()
    {
        if (string.IsNullOrEmpty(_startNodeId))
        {
            throw new InvalidOperationException("Workflow must specify a start node");
        }

        return new WorkflowDefinition
        {
            Id = _id,
            Name = _name,
            Description = _description,
            Version = _version,
            Nodes = _nodes,
            Edges = _edges,
            StartNodeId = _startNodeId,
            Metadata = _metadata.Count > 0 ? _metadata : null,
        };
    }
}

/// <summary>
/// Condition node builder.
/// </summary>
public sealed class ConditionNodeBuilder
{
    private string? _inputSource;
    private readonly List<Dictionary<string, object?>> _branches = [];
    private string? _defaultBranchNodeId;

    public ConditionNodeBuilder InputFrom(string source)
    {
        _inputSource = source;
        return this;
    }

    public ConditionNodeBuilder AddBranch(string name, string condition, string targetNodeId)
    {
        _branches.Add(
            new Dictionary<string, object?>
            {
                ["name"] = name,
                ["condition"] = condition,
                ["targetNodeId"] = targetNodeId,
            }
        );
        return this;
    }

    public ConditionNodeBuilder DefaultTo(string nodeId)
    {
        _defaultBranchNodeId = nodeId;
        return this;
    }

    internal Dictionary<string, object?> Build()
    {
        return new Dictionary<string, object?>
        {
            ["inputSource"] = _inputSource,
            ["branches"] = _branches,
            ["defaultBranchNodeId"] = _defaultBranchNodeId,
        };
    }
}

/// <summary>
/// Parallel node builder.
/// </summary>
public sealed class ParallelNodeBuilder
{
    private readonly List<Dictionary<string, object?>> _branches = [];
    private ParallelWaitStrategy _waitStrategy = ParallelWaitStrategy.WaitAll;
    private ParallelMergeStrategy _mergeStrategy = ParallelMergeStrategy.Concatenate;
    private int? _branchTimeoutMs;

    public ParallelNodeBuilder AddBranch(string name, string startNodeId)
    {
        _branches.Add(
            new Dictionary<string, object?> { ["name"] = name, ["startNodeId"] = startNodeId }
        );
        return this;
    }

    public ParallelNodeBuilder WaitAll()
    {
        _waitStrategy = ParallelWaitStrategy.WaitAll;
        return this;
    }

    public ParallelNodeBuilder WaitAny()
    {
        _waitStrategy = ParallelWaitStrategy.WaitAny;
        return this;
    }

    public ParallelNodeBuilder ConcatenateResults()
    {
        _mergeStrategy = ParallelMergeStrategy.Concatenate;
        return this;
    }

    public ParallelNodeBuilder TakeFirst()
    {
        _mergeStrategy = ParallelMergeStrategy.First;
        return this;
    }

    public ParallelNodeBuilder WithTimeout(int timeoutMs)
    {
        _branchTimeoutMs = timeoutMs;
        return this;
    }

    internal Dictionary<string, object?> Build()
    {
        return new Dictionary<string, object?>
        {
            ["branches"] = _branches,
            ["waitStrategy"] = _waitStrategy.ToString(),
            ["mergeStrategy"] = _mergeStrategy.ToString(),
            ["branchTimeoutMs"] = _branchTimeoutMs,
        };
    }
}
