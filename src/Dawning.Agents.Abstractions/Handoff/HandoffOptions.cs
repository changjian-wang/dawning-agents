namespace Dawning.Agents.Abstractions.Handoff;

/// <summary>
/// Handoff 配置选项
/// </summary>
public class HandoffOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Handoff";

    /// <summary>
    /// 最大 Handoff 深度（防止无限循环）
    /// </summary>
    /// <remarks>默认为 5，表示最多可以连续转交 5 次</remarks>
    public int MaxHandoffDepth { get; set; } = 5;

    /// <summary>
    /// 单次 Handoff 超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 总超时时间（秒）
    /// </summary>
    public int TotalTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 是否允许回环（Agent A -> B -> A）
    /// </summary>
    public bool AllowCycles { get; set; } = false;

    /// <summary>
    /// Handoff 失败时是否回退到源 Agent
    /// </summary>
    public bool FallbackToSource { get; set; } = true;

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (MaxHandoffDepth < 1)
        {
            throw new InvalidOperationException("MaxHandoffDepth must be at least 1");
        }

        if (TimeoutSeconds < 1)
        {
            throw new InvalidOperationException("TimeoutSeconds must be at least 1");
        }

        if (TotalTimeoutSeconds < TimeoutSeconds)
        {
            throw new InvalidOperationException("TotalTimeoutSeconds must be >= TimeoutSeconds");
        }
    }
}
