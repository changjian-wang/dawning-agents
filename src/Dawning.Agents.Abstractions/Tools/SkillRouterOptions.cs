namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 语义技能路由器配置
/// </summary>
public sealed class SkillRouterOptions : IValidatableOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "SkillRouter";

    /// <summary>
    /// 启用语义路由的工具数量阈值（低于此值全量注入）
    /// </summary>
    public int ActivationThreshold { get; set; } = 10;

    /// <summary>
    /// 默认 top-K
    /// </summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// 默认最小相似度
    /// </summary>
    public float DefaultMinScore { get; set; } = 0.3f;

    /// <inheritdoc />
    public void Validate()
    {
        if (ActivationThreshold < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(ActivationThreshold)} must be at least 1, got {ActivationThreshold}"
            );
        }

        if (DefaultTopK < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(DefaultTopK)} must be at least 1, got {DefaultTopK}"
            );
        }

        if (DefaultMinScore is < 0f or > 1f)
        {
            throw new InvalidOperationException(
                $"{nameof(DefaultMinScore)} must be between 0 and 1, got {DefaultMinScore}"
            );
        }
    }
}
