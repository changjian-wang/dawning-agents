namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// 反思引擎配置
/// </summary>
public sealed class ReflectionOptions : IValidatableOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Reflection";

    /// <summary>
    /// 是否启用反思引擎
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 同一工具连续失败多少次后触发反思
    /// </summary>
    public int FailureThreshold { get; set; } = 2;

    /// <summary>
    /// 最大反思次数（防止无限修复循环）
    /// </summary>
    public int MaxReflections { get; set; } = 3;

    /// <summary>
    /// 反思使用的模型（可使用较便宜的模型，null 表示使用默认模型）
    /// </summary>
    public string? ModelOverride { get; set; }

    /// <inheritdoc />
    public void Validate()
    {
        if (FailureThreshold < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(FailureThreshold)} must be at least 1, got {FailureThreshold}"
            );
        }

        if (MaxReflections < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(MaxReflections)} must be at least 1, got {MaxReflections}"
            );
        }
    }
}
