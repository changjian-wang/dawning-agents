namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// 审批配置
/// </summary>
public class ApprovalConfig
{
    /// <summary>
    /// 是否对低风险操作要求审批
    /// </summary>
    public bool RequireApprovalForLowRisk { get; set; } = false;

    /// <summary>
    /// 是否对中等风险操作要求审批
    /// </summary>
    public bool RequireApprovalForMediumRisk { get; set; } = true;

    /// <summary>
    /// 审批超时时间
    /// </summary>
    public TimeSpan ApprovalTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 超时时的默认操作（approve/reject）
    /// </summary>
    public string DefaultOnTimeout { get; set; } = "reject";
}

/// <summary>
/// 人机协作配置
/// </summary>
public class HumanLoopOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "HumanLoop";

    /// <summary>
    /// 是否在执行前确认
    /// </summary>
    public bool ConfirmBeforeExecution { get; set; } = false;

    /// <summary>
    /// 是否在返回前审查
    /// </summary>
    public bool ReviewBeforeReturn { get; set; } = false;

    /// <summary>
    /// 是否对中等风险操作要求审批
    /// </summary>
    public bool RequireApprovalForMediumRisk { get; set; } = true;

    /// <summary>
    /// 默认超时时间
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 高风险关键词（用于自动识别风险级别）
    /// </summary>
    public string[] HighRiskKeywords { get; set; } =
    [
        "delete",
        "remove",
        "destroy",
        "execute",
        "transfer",
        "payment",
        "删除",
        "移除",
        "执行",
        "转账",
        "支付",
    ];

    /// <summary>
    /// 关键风险关键词
    /// </summary>
    public string[] CriticalRiskKeywords { get; set; } =
    ["production", "financial", "customer data", "credentials", "生产", "财务", "客户数据", "凭证"];
}
