namespace Dawning.Agents.Abstractions.Workflow;

/// <summary>
/// 工作流定义（可序列化）
/// </summary>
public record WorkflowDefinition
{
    /// <summary>
    /// 工作流 ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 工作流名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 工作流描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// 节点定义列表
    /// </summary>
    public List<WorkflowNodeDefinition> Nodes { get; init; } = [];

    /// <summary>
    /// 边（连接）定义列表
    /// </summary>
    public List<WorkflowEdgeDefinition> Edges { get; init; } = [];

    /// <summary>
    /// 起始节点 ID
    /// </summary>
    public required string StartNodeId { get; init; }

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object?>? Metadata { get; init; }
}

/// <summary>
/// 节点定义（可序列化）
/// </summary>
public record WorkflowNodeDefinition
{
    /// <summary>
    /// 节点 ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 节点名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 节点类型
    /// </summary>
    public WorkflowNodeType Type { get; init; }

    /// <summary>
    /// 节点配置（JSON 格式，根据 Type 解析为具体配置类）
    /// </summary>
    public Dictionary<string, object?>? Config { get; init; }

    /// <summary>
    /// 可视化位置 X
    /// </summary>
    public double? PositionX { get; init; }

    /// <summary>
    /// 可视化位置 Y
    /// </summary>
    public double? PositionY { get; init; }

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object?>? Metadata { get; init; }
}

/// <summary>
/// 边（连接）定义（可序列化）
/// </summary>
public record WorkflowEdgeDefinition
{
    /// <summary>
    /// 源节点 ID
    /// </summary>
    public required string FromNodeId { get; init; }

    /// <summary>
    /// 目标节点 ID
    /// </summary>
    public required string ToNodeId { get; init; }

    /// <summary>
    /// 边标签（用于条件分支）
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// 条件表达式（可选）
    /// </summary>
    public string? Condition { get; init; }
}

/// <summary>
/// 工作流序列化器接口
/// </summary>
public interface IWorkflowSerializer
{
    /// <summary>
    /// 序列化为 JSON
    /// </summary>
    string SerializeToJson(WorkflowDefinition definition);

    /// <summary>
    /// 从 JSON 反序列化
    /// </summary>
    WorkflowDefinition DeserializeFromJson(string json);

    /// <summary>
    /// 序列化为 YAML
    /// </summary>
    string SerializeToYaml(WorkflowDefinition definition);

    /// <summary>
    /// 从 YAML 反序列化
    /// </summary>
    WorkflowDefinition DeserializeFromYaml(string yaml);
}

/// <summary>
/// 工作流可视化器接口
/// </summary>
public interface IWorkflowVisualizer
{
    /// <summary>
    /// 生成 Mermaid 图表
    /// </summary>
    string GenerateMermaid(WorkflowDefinition definition);

    /// <summary>
    /// 生成 DOT 图表（Graphviz）
    /// </summary>
    string GenerateDot(WorkflowDefinition definition);
}

/// <summary>
/// 工作流引擎接口
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// 从定义创建工作流
    /// </summary>
    IWorkflow CreateWorkflow(WorkflowDefinition definition);

    /// <summary>
    /// 执行工作流定义
    /// </summary>
    Task<WorkflowResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowContext context,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 验证工作流定义
    /// </summary>
    WorkflowValidationResult Validate(WorkflowDefinition definition);
}

/// <summary>
/// 工作流验证结果
/// </summary>
public record WorkflowValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 错误列表
    /// </summary>
    public List<WorkflowValidationError> Errors { get; init; } = [];

    /// <summary>
    /// 警告列表
    /// </summary>
    public List<WorkflowValidationWarning> Warnings { get; init; } = [];
}

/// <summary>
/// 工作流验证错误
/// </summary>
public record WorkflowValidationError
{
    /// <summary>
    /// 错误代码
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 相关节点 ID
    /// </summary>
    public string? NodeId { get; init; }
}

/// <summary>
/// 工作流验证警告
/// </summary>
public record WorkflowValidationWarning
{
    /// <summary>
    /// 警告代码
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// 警告消息
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 相关节点 ID
    /// </summary>
    public string? NodeId { get; init; }
}
