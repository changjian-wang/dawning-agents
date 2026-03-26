namespace Dawning.Agents.Abstractions.Workflow;

/// <summary>
/// Workflow definition (serializable)
/// </summary>
public record WorkflowDefinition
{
    /// <summary>
    /// Workflow ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Workflow name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Workflow description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Version number
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Node definition list
    /// </summary>
    public IReadOnlyList<WorkflowNodeDefinition> Nodes { get; init; } = [];

    /// <summary>
    /// Edge (connection) definition list
    /// </summary>
    public IReadOnlyList<WorkflowEdgeDefinition> Edges { get; init; } = [];

    /// <summary>
    /// Start node ID
    /// </summary>
    public required string StartNodeId { get; init; }

    /// <summary>
    /// Metadata
    /// </summary>
    public Dictionary<string, object?>? Metadata { get; init; }
}

/// <summary>
/// Node definition (serializable)
/// </summary>
public record WorkflowNodeDefinition
{
    /// <summary>
    /// Node ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Node name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Node type
    /// </summary>
    public WorkflowNodeType Type { get; init; }

    /// <summary>
    /// Node configuration (JSON format, parsed into specific config classes based on Type)
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Config { get; init; }

    /// <summary>
    /// Visual position X
    /// </summary>
    public double? PositionX { get; init; }

    /// <summary>
    /// Visual position Y
    /// </summary>
    public double? PositionY { get; init; }

    /// <summary>
    /// Metadata
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}

/// <summary>
/// Edge (connection) definition (serializable)
/// </summary>
public record WorkflowEdgeDefinition
{
    /// <summary>
    /// Source node ID.
    /// </summary>
    public required string FromNodeId { get; init; }

    /// <summary>
    /// Target node ID.
    /// </summary>
    public required string ToNodeId { get; init; }

    /// <summary>
    /// Edge label (for conditional branching)
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Condition expression (optional)
    /// </summary>
    public string? Condition { get; init; }
}

/// <summary>
/// Workflow serializer interface
/// </summary>
public interface IWorkflowSerializer
{
    /// <summary>
    /// Serialize to JSON
    /// </summary>
    string SerializeToJson(WorkflowDefinition definition);

    /// <summary>
    /// Deserialize from JSON
    /// </summary>
    WorkflowDefinition DeserializeFromJson(string json);

    /// <summary>
    /// Serialize to YAML
    /// </summary>
    string SerializeToYaml(WorkflowDefinition definition);

    /// <summary>
    /// Deserialize from YAML
    /// </summary>
    WorkflowDefinition DeserializeFromYaml(string yaml);
}

/// <summary>
/// Workflow visualizer interface
/// </summary>
public interface IWorkflowVisualizer
{
    /// <summary>
    /// Generate a Mermaid diagram
    /// </summary>
    string GenerateMermaid(WorkflowDefinition definition);

    /// <summary>
    /// Generate a DOT diagram (Graphviz)
    /// </summary>
    string GenerateDot(WorkflowDefinition definition);
}

/// <summary>
/// Workflow engine interface
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Create a workflow from a definition
    /// </summary>
    IWorkflow CreateWorkflow(WorkflowDefinition definition);

    /// <summary>
    /// Execute a workflow definition
    /// </summary>
    Task<WorkflowResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowContext context,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validate a workflow definition
    /// </summary>
    WorkflowValidationResult Validate(WorkflowDefinition definition);
}

/// <summary>
/// Workflow validation result
/// </summary>
public record WorkflowValidationResult
{
    /// <summary>
    /// Whether valid
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Error list
    /// </summary>
    public List<WorkflowValidationError> Errors { get; init; } = [];

    /// <summary>
    /// Warning list
    /// </summary>
    public List<WorkflowValidationWarning> Warnings { get; init; } = [];
}

/// <summary>
/// Workflow validation error
/// </summary>
public record WorkflowValidationError
{
    /// <summary>
    /// Error code
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Error message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Related node ID
    /// </summary>
    public string? NodeId { get; init; }
}

/// <summary>
/// Workflow validation warning
/// </summary>
public record WorkflowValidationWarning
{
    /// <summary>
    /// Warning code
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Warning message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Related node ID
    /// </summary>
    public string? NodeId { get; init; }
}
