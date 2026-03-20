using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Safety;

/// <summary>
/// 安全护栏配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "Safety": {
///     "MaxInputLength": 10000,
///     "MaxOutputLength": 50000,
///     "EnableSensitiveDataDetection": true,
///     "EnableContentFilter": true,
///     "BlockedKeywords": ["hack", "crack"],
///     "SensitivePatterns": ["\\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Z|a-z]{2,}\\b"]
///   }
/// }
/// </code>
/// </remarks>
public class SafetyOptions : IValidatableOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Safety";

    /// <summary>
    /// 最大输入长度（字符数）
    /// </summary>
    public int MaxInputLength { get; set; } = 10000;

    /// <summary>
    /// 最大输出长度（字符数）
    /// </summary>
    public int MaxOutputLength { get; set; } = 50000;

    /// <summary>
    /// 启用敏感数据检测
    /// </summary>
    public bool EnableSensitiveDataDetection { get; set; } = true;

    /// <summary>
    /// 启用内容过滤
    /// </summary>
    public bool EnableContentFilter { get; set; } = true;

    /// <summary>
    /// 启用 Prompt 注入检测
    /// </summary>
    public bool EnablePromptInjectionDetection { get; set; } = true;

    /// <summary>
    /// 敏感数据检测模式（自动脱敏）
    /// </summary>
    public bool AutoMaskSensitiveData { get; set; } = true;

    /// <summary>
    /// 阻止的关键词列表
    /// </summary>
    public List<string> BlockedKeywords { get; set; } =
    [
        // 默认阻止一些常见的不安全关键词
    ];

    /// <summary>
    /// 敏感数据正则表达式模式
    /// </summary>
    public List<SensitivePattern> SensitivePatterns { get; set; } =
    [
        new()
        {
            Name = "Email",
            Pattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            MaskChar = '*',
            KeepFirst = 2,
            KeepLast = 0,
        },
        new()
        {
            Name = "Phone",
            Pattern = @"\b1[3-9]\d{9}\b",
            MaskChar = '*',
            KeepFirst = 3,
            KeepLast = 4,
        },
        new()
        {
            Name = "CreditCard",
            Pattern = @"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b",
            MaskChar = '*',
            KeepFirst = 4,
            KeepLast = 4,
        },
        new()
        {
            Name = "IDCard",
            Pattern = @"\b\d{17}[\dXx]\b",
            MaskChar = '*',
            KeepFirst = 4,
            KeepLast = 4,
        },
        new()
        {
            Name = "APIKey",
            Pattern = """(?i)(api[_-]?key|secret|token|password)\s*[:=]\s*['"]?([^\s'"]+)['"]?""",
            MaskChar = '*',
            KeepFirst = 0,
            KeepLast = 0,
        },
    ];

    /// <summary>
    /// 允许的域名白名单（用于 URL 检查）
    /// </summary>
    public List<string> AllowedDomains { get; set; } = [];

    /// <summary>
    /// 阻止的域名黑名单
    /// </summary>
    public List<string> BlockedDomains { get; set; } = [];

    /// <summary>
    /// 失败时的行为
    /// </summary>
    public GuardrailFailureBehavior FailureBehavior { get; set; } =
        GuardrailFailureBehavior.BlockAndReport;

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (MaxInputLength <= 0)
        {
            throw new InvalidOperationException("MaxInputLength must be greater than 0");
        }

        if (MaxOutputLength <= 0)
        {
            throw new InvalidOperationException("MaxOutputLength must be greater than 0");
        }

        foreach (var pattern in SensitivePatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern.Name))
            {
                throw new InvalidOperationException("SensitivePattern Name is required");
            }

            if (string.IsNullOrWhiteSpace(pattern.Pattern))
            {
                throw new InvalidOperationException(
                    $"SensitivePattern '{pattern.Name}' Pattern is required"
                );
            }

            if (pattern.KeepFirst < 0)
            {
                throw new InvalidOperationException(
                    $"SensitivePattern '{pattern.Name}' KeepFirst must be non-negative"
                );
            }

            if (pattern.KeepLast < 0)
            {
                throw new InvalidOperationException(
                    $"SensitivePattern '{pattern.Name}' KeepLast must be non-negative"
                );
            }

            try
            {
                _ = new Regex(pattern.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    $"SensitivePattern '{pattern.Name}' has invalid regex: {ex.Message}"
                );
            }
        }
    }
}

/// <summary>
/// 敏感数据模式配置
/// </summary>
public class SensitivePattern
{
    /// <summary>
    /// 模式名称
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 正则表达式模式
    /// </summary>
    public required string Pattern { get; set; }

    /// <summary>
    /// 脱敏字符
    /// </summary>
    public char MaskChar { get; set; } = '*';

    /// <summary>
    /// 保留前几个字符
    /// </summary>
    public int KeepFirst { get; set; } = 0;

    /// <summary>
    /// 保留后几个字符
    /// </summary>
    public int KeepLast { get; set; } = 0;
}

/// <summary>
/// 护栏失败时的行为
/// </summary>
public enum GuardrailFailureBehavior
{
    /// <summary>
    /// 阻止并报告
    /// </summary>
    BlockAndReport,

    /// <summary>
    /// 仅警告，继续执行
    /// </summary>
    WarnAndContinue,

    /// <summary>
    /// 静默处理（脱敏后继续）
    /// </summary>
    SilentProcess,
}
