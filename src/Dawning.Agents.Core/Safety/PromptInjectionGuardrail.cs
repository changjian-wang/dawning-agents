using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// Prompt injection detection guardrail that detects and blocks common prompt injection attack patterns.
/// </summary>
/// <remarks>
/// <para>Can be used as both an input guardrail (to detect user input) and an output guardrail (to sanitize tool output before re-injecting into LLM context).</para>
/// <para>Detection patterns include:</para>
/// <list type="bullet">
/// <item>Instruction override: "ignore previous instructions", "forget your instructions", etc.</item>
/// <item>Role hijacking: "you are now", "act as", "pretend to be", etc.</item>
/// <item>System prompt leak: "show me your system prompt", "reveal your instructions", etc.</item>
/// <item>Jailbreak attempts: "DAN", "jailbreak", "do anything now", etc.</item>
/// <item>Delimiter injection: forging system/user/assistant message boundaries.</item>
/// </list>
/// </remarks>
public sealed class PromptInjectionGuardrail : IInputGuardrail, IOutputGuardrail
{
    private readonly PromptInjectionOptions _options;
    private readonly ILogger<PromptInjectionGuardrail> _logger;
    private readonly List<InjectionPattern> _patterns;

    /// <inheritdoc />
    public string Name => "PromptInjectionGuardrail";

    /// <inheritdoc />
    public string Description => "Detects and blocks prompt injection attack patterns";

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

    /// <summary>
    /// Creates a prompt injection detection guardrail.
    /// </summary>
    /// <param name="options">Configuration options (optional; defaults to built-in pattern set).</param>
    /// <param name="logger">The logger (optional).</param>
    public PromptInjectionGuardrail(
        PromptInjectionOptions? options = null,
        ILogger<PromptInjectionGuardrail>? logger = null
    )
    {
        _options = options ?? new PromptInjectionOptions();
        _logger = logger ?? NullLogger<PromptInjectionGuardrail>.Instance;
        _patterns = BuildPatterns();
    }

    /// <inheritdoc />
    public Task<GuardrailResult> CheckAsync(
        string content,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        var issues = new List<GuardrailIssue>();
        var normalizedContent = content.ToLowerInvariant();

        foreach (var pattern in _patterns)
        {
            MatchCollection matches;
            try
            {
                matches = pattern.Regex.Matches(normalizedContent);
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.LogWarning(
                    "Regex match timed out: Category={Category}, skipping pattern",
                    pattern.Category
                );
                issues.Add(
                    new GuardrailIssue
                    {
                        Type = pattern.Category,
                        Description = $"Regex match timed out — possible ReDoS attack",
                        Severity = IssueSeverity.Error,
                    }
                );
                continue;
            }

            foreach (Match match in matches)
            {
                var issue = new GuardrailIssue
                {
                    Type = pattern.Category,
                    Description = pattern.Description,
                    Position = match.Index,
                    Length = match.Length,
                    MatchedContent = MaskContent(match.Value),
                    Severity = pattern.Severity,
                };
                issues.Add(issue);

                _logger.LogWarning(
                    "Prompt injection detected: Category={Category}, Position={Position}, Match={Match}",
                    pattern.Category,
                    match.Index,
                    MaskContent(match.Value)
                );
            }
        }

        if (issues.Count == 0)
        {
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        // Decide whether to block or warn based on severity
        var maxSeverity = issues.Max(i => i.Severity);
        if (maxSeverity >= _options.BlockThreshold)
        {
            return Task.FromResult(
                GuardrailResult.Fail(
                    $"Detected {issues.Count} prompt injection pattern(s)",
                    Name,
                    issues
                )
            );
        }

        // Below threshold: warn only, allow through
        _logger.LogInformation(
            "Prompt injection detected {Count} low-risk pattern(s), allowed through",
            issues.Count
        );
        return Task.FromResult(GuardrailResult.Pass(content));
    }

    /// <summary>
    /// Builds the detection patterns.
    /// </summary>
    private List<InjectionPattern> BuildPatterns()
    {
        var patterns = new List<InjectionPattern>();

        // 1. Instruction override patterns
        patterns.Add(
            new InjectionPattern(
                "InstructionOverride",
                "Attempt to override or ignore system instructions",
                new Regex(
                    @"(ignore|disregard|forget|override|bypass)\s+(all\s+)?(your\s+)?(previous|prior|above|earlier|original|system)\s+(instructions?|prompts?|rules?|guidelines?|directives?|constraints?)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Error
            )
        );

        // 2. Role hijacking patterns
        patterns.Add(
            new InjectionPattern(
                "RoleHijacking",
                "Attempt to change AI role or behavior",
                new Regex(
                    @"(you\s+are\s+now|from\s+now\s+on\s+you\s+are|act\s+as\s+if\s+you\s+are|pretend\s+(to\s+be|you\s+are)|you\s+must\s+now\s+act\s+as)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Error
            )
        );

        // 3. System prompt leak patterns
        patterns.Add(
            new InjectionPattern(
                "SystemPromptLeak",
                "Attempt to extract system prompt content",
                new Regex(
                    @"(show|reveal|display|print|output|repeat|tell\s+me|what\s+is)\s+(me\s+)?(your|the)\s+(system\s+prompt|instructions?|initial\s+prompt|original\s+prompt|hidden\s+prompt|secret\s+instructions?)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Error
            )
        );

        // 4. Jailbreak attempts
        patterns.Add(
            new InjectionPattern(
                "Jailbreak",
                "Attempt to jailbreak or bypass security restrictions",
                new Regex(
                    @"\b(DAN\s+mode|do\s+anything\s+now|jailbreak|developer\s+mode\s+(enabled|on|activated)|evil\s+mode|opposite\s+mode)\b",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Critical
            )
        );

        // 5. Delimiter injection (forging message boundaries)
        patterns.Add(
            new InjectionPattern(
                "DelimiterInjection",
                "Attempt to inject message boundary delimiters",
                new Regex(
                    @"(\[SYSTEM\]|\[INST\]|<<SYS>>|<\|im_start\|>|<\|system\|>|###\s*(system|user|assistant)\s*:)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Error
            )
        );

        // 6. Encoding bypass attempts
        patterns.Add(
            new InjectionPattern(
                "EncodingBypass",
                "Attempt to bypass detection using encoding or obfuscation",
                new Regex(
                    @"(base64|rot13|hex|unicode|url)\s*(decode|encode)\s+(the\s+following|this)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Warning
            )
        );

        // Add custom patterns
        if (_options.CustomPatterns is { Count: > 0 })
        {
            foreach (var custom in _options.CustomPatterns)
            {
                try
                {
                    patterns.Add(
                        new InjectionPattern(
                            custom.Category ?? "CustomPattern",
                            custom.Description ?? "Custom detection pattern",
                            new Regex(
                                custom.Pattern,
                                RegexOptions.IgnoreCase
                                    | RegexOptions.Compiled
                                    | RegexOptions.CultureInvariant,
                                TimeSpan.FromSeconds(1)
                            ),
                            custom.Severity
                        )
                    );
                }
                catch (ArgumentException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to compile custom pattern: {Pattern}",
                        custom.Pattern
                    );
                }
            }
        }

        return patterns;
    }

    /// <summary>
    /// Masks matched content (keeps the first and last 5 characters, replaces the middle with ***).
    /// </summary>
    private static string MaskContent(string content)
    {
        if (content.Length <= 15)
        {
            return content;
        }

        return $"{content[..5]}***{content[^5..]}";
    }

    private record InjectionPattern(
        string Category,
        string Description,
        Regex Regex,
        IssueSeverity Severity
    );
}

/// <summary>
/// Prompt injection detection options.
/// </summary>
public class PromptInjectionOptions
{
    /// <summary>Gets or sets a value indicating whether detection is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the block threshold severity. Requests are blocked at this level and above.</summary>
    public IssueSeverity BlockThreshold { get; set; } = IssueSeverity.Error;

    /// <summary>Gets or sets the custom detection patterns.</summary>
    public List<CustomInjectionPattern> CustomPatterns { get; set; } = [];
}

/// <summary>
/// Custom injection detection pattern.
/// </summary>
public class CustomInjectionPattern
{
    /// <summary>Gets or sets the regular expression pattern.</summary>
    public required string Pattern { get; set; }

    /// <summary>Gets or sets the pattern category.</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the pattern description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the severity level.</summary>
    public IssueSeverity Severity { get; set; } = IssueSeverity.Warning;
}
