using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Safety;

/// <summary>
/// Safety guardrail configuration options.
/// </summary>
/// <remarks>
/// appsettings.json example:
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
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Safety";

    /// <summary>
    /// Maximum input length (in characters).
    /// </summary>
    public int MaxInputLength { get; set; } = 10000;

    /// <summary>
    /// Maximum output length (in characters).
    /// </summary>
    public int MaxOutputLength { get; set; } = 50000;

    /// <summary>
    /// Enable sensitive data detection.
    /// </summary>
    public bool EnableSensitiveDataDetection { get; set; } = true;

    /// <summary>
    /// Enable content filtering.
    /// </summary>
    public bool EnableContentFilter { get; set; } = true;

    /// <summary>
    /// Enable prompt injection detection.
    /// </summary>
    public bool EnablePromptInjectionDetection { get; set; } = true;

    /// <summary>
    /// Sensitive data detection mode (auto-masking).
    /// </summary>
    public bool AutoMaskSensitiveData { get; set; } = true;

    /// <summary>
    /// List of blocked keywords.
    /// </summary>
    public List<string> BlockedKeywords { get; set; } =
    [
        // Default blocked unsafe keywords
    ];

    /// <summary>
    /// Sensitive data regex patterns.
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
    /// Allowed domain whitelist (for URL checking).
    /// </summary>
    public List<string> AllowedDomains { get; set; } = [];

    /// <summary>
    /// Blocked domain blacklist.
    /// </summary>
    public List<string> BlockedDomains { get; set; } = [];

    /// <summary>
    /// Behavior on guardrail failure.
    /// </summary>
    public GuardrailFailureBehavior FailureBehavior { get; set; } =
        GuardrailFailureBehavior.BlockAndReport;

    /// <summary>
    /// Validates the configuration.
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
/// Sensitive data pattern configuration.
/// </summary>
public class SensitivePattern
{
    /// <summary>
    /// Pattern name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Regex pattern.
    /// </summary>
    public required string Pattern { get; set; }

    /// <summary>
    /// Masking character.
    /// </summary>
    public char MaskChar { get; set; } = '*';

    /// <summary>
    /// Number of leading characters to preserve.
    /// </summary>
    public int KeepFirst { get; set; } = 0;

    /// <summary>
    /// Number of trailing characters to preserve.
    /// </summary>
    public int KeepLast { get; set; } = 0;
}

/// <summary>
/// Guardrail failure behavior.
/// </summary>
public enum GuardrailFailureBehavior
{
    /// <summary>
    /// Block and report.
    /// </summary>
    BlockAndReport,

    /// <summary>
    /// Warn only, continue execution.
    /// </summary>
    WarnAndContinue,

    /// <summary>
    /// Silent processing (mask and continue).
    /// </summary>
    SilentProcess,
}
