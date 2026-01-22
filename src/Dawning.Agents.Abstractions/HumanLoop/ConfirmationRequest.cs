namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// 人工确认请求
/// </summary>
public record ConfirmationRequest
{
    /// <summary>
    /// 唯一请求标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 需要的确认类型
    /// </summary>
    public ConfirmationType Type { get; init; } = ConfirmationType.Binary;

    /// <summary>
    /// 需要确认的操作
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// 详细描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 操作的风险级别
    /// </summary>
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Medium;

    /// <summary>
    /// 供人类选择的选项
    /// </summary>
    public IReadOnlyList<ConfirmationOption> Options { get; init; } = [];

    /// <summary>
    /// 用于决策的上下文数据
    /// </summary>
    public IDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// 请求创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 确认超时时间
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// 超时时的默认操作
    /// </summary>
    public string? DefaultOnTimeout { get; init; }
}

/// <summary>
/// 确认类型
/// </summary>
public enum ConfirmationType
{
    /// <summary>
    /// 是/否二元选择
    /// </summary>
    Binary,

    /// <summary>
    /// 多选项
    /// </summary>
    MultiChoice,

    /// <summary>
    /// 用户自由输入
    /// </summary>
    FreeformInput,

    /// <summary>
    /// 审查和修改
    /// </summary>
    Review,
}

/// <summary>
/// 风险级别
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// 低风险 - 通常自动批准
    /// </summary>
    Low,

    /// <summary>
    /// 中等风险 - 可能需要确认
    /// </summary>
    Medium,

    /// <summary>
    /// 高风险 - 需要确认
    /// </summary>
    High,

    /// <summary>
    /// 关键风险 - 必须确认
    /// </summary>
    Critical,
}

/// <summary>
/// 确认选项
/// </summary>
public record ConfirmationOption
{
    /// <summary>
    /// 选项唯一标识
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 选项显示标签
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// 选项描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 是否为默认选项
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// 是否为危险操作
    /// </summary>
    public bool IsDangerous { get; init; }
}
