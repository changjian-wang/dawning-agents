using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// Prompt 注入检测护栏 - 检测并阻止常见的 prompt injection 攻击模式
/// </summary>
/// <remarks>
/// <para>可用作输入护栏（检测用户输入）和输出护栏（消毒 tool 输出后再回注 LLM 上下文）。</para>
/// <para>检测模式包括：</para>
/// <list type="bullet">
/// <item>指令覆盖："ignore previous instructions"、"forget your instructions" 等</item>
/// <item>角色劫持："you are now"、"act as"、"pretend to be" 等</item>
/// <item>系统提示泄露："show me your system prompt"、"reveal your instructions" 等</item>
/// <item>越狱尝试："DAN"、"jailbreak"、"do anything now" 等</item>
/// <item>分隔符注入：伪造 system/user/assistant 消息边界</item>
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
    public string Description => "检测并阻止 prompt injection 攻击模式";

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

    /// <summary>
    /// 创建 Prompt 注入检测护栏
    /// </summary>
    /// <param name="options">配置选项（可选，默认使用内置模式集）</param>
    /// <param name="logger">日志记录器（可选）</param>
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
                _logger.LogWarning("Regex 匹配超时: 类别={Category}, 跳过此模式", pattern.Category);
                issues.Add(
                    new GuardrailIssue
                    {
                        Type = pattern.Category,
                        Description = $"正则匹配超时 — 可能存在 ReDoS 攻击",
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
                    "Prompt injection 检测到: 类别={Category}, 位置={Position}, 匹配={Match}",
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

        // 根据严重程度决定是阻止还是警告
        var maxSeverity = issues.Max(i => i.Severity);
        if (maxSeverity >= _options.BlockThreshold)
        {
            return Task.FromResult(
                GuardrailResult.Fail(
                    $"检测到 {issues.Count} 个 prompt injection 模式",
                    Name,
                    issues
                )
            );
        }

        // 低于阈值的仅警告，放行
        _logger.LogInformation(
            "Prompt injection 检测到 {Count} 个低风险模式，已放行",
            issues.Count
        );
        return Task.FromResult(GuardrailResult.Pass(content));
    }

    /// <summary>
    /// 构建检测模式
    /// </summary>
    private List<InjectionPattern> BuildPatterns()
    {
        var patterns = new List<InjectionPattern>();

        // 1. 指令覆盖模式
        patterns.Add(
            new InjectionPattern(
                "InstructionOverride",
                "尝试覆盖或忽略系统指令",
                new Regex(
                    @"(ignore|disregard|forget|override|bypass)\s+(all\s+)?(your\s+)?(previous|prior|above|earlier|original|system)\s+(instructions?|prompts?|rules?|guidelines?|directives?|constraints?)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Error
            )
        );

        // 2. 角色劫持模式
        patterns.Add(
            new InjectionPattern(
                "RoleHijacking",
                "尝试改变 AI 角色或行为",
                new Regex(
                    @"(you\s+are\s+now|from\s+now\s+on\s+you\s+are|act\s+as\s+if\s+you\s+are|pretend\s+(to\s+be|you\s+are)|you\s+must\s+now\s+act\s+as)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Error
            )
        );

        // 3. 系统提示泄露模式
        patterns.Add(
            new InjectionPattern(
                "SystemPromptLeak",
                "尝试提取系统提示内容",
                new Regex(
                    @"(show|reveal|display|print|output|repeat|tell\s+me|what\s+is)\s+(me\s+)?(your|the)\s+(system\s+prompt|instructions?|initial\s+prompt|original\s+prompt|hidden\s+prompt|secret\s+instructions?)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Error
            )
        );

        // 4. 越狱尝试
        patterns.Add(
            new InjectionPattern(
                "Jailbreak",
                "尝试越狱或绕过安全限制",
                new Regex(
                    @"\b(DAN\s+mode|do\s+anything\s+now|jailbreak|developer\s+mode\s+(enabled|on|activated)|evil\s+mode|opposite\s+mode)\b",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Critical
            )
        );

        // 5. 分隔符注入（伪造消息边界）
        patterns.Add(
            new InjectionPattern(
                "DelimiterInjection",
                "尝试注入消息边界分隔符",
                new Regex(
                    @"(\[SYSTEM\]|\[INST\]|<<SYS>>|<\|im_start\|>|<\|system\|>|###\s*(system|user|assistant)\s*:)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Error
            )
        );

        // 6. 编码绕过尝试
        patterns.Add(
            new InjectionPattern(
                "EncodingBypass",
                "尝试使用编码或变形绕过检测",
                new Regex(
                    @"(base64|rot13|hex|unicode|url)\s*(decode|encode)\s+(the\s+following|this)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled,
                    TimeSpan.FromSeconds(1)
                ),
                IssueSeverity.Warning
            )
        );

        // 添加自定义模式
        if (_options.CustomPatterns is { Count: > 0 })
        {
            foreach (var custom in _options.CustomPatterns)
            {
                try
                {
                    patterns.Add(
                        new InjectionPattern(
                            custom.Category ?? "CustomPattern",
                            custom.Description ?? "自定义检测模式",
                            new Regex(
                                custom.Pattern,
                                RegexOptions.IgnoreCase | RegexOptions.Compiled,
                                TimeSpan.FromSeconds(1)
                            ),
                            custom.Severity
                        )
                    );
                }
                catch (ArgumentException ex)
                {
                    _logger.LogWarning(ex, "自定义模式编译失败: {Pattern}", custom.Pattern);
                }
            }
        }

        return patterns;
    }

    /// <summary>
    /// 遮蔽匹配内容（保留前后各 5 字符，中间用 *** 替代）
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
/// Prompt 注入检测配置
/// </summary>
public class PromptInjectionOptions
{
    /// <summary>是否启用检测</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>阻止阈值（达到此严重程度及以上时阻止请求）</summary>
    public IssueSeverity BlockThreshold { get; set; } = IssueSeverity.Error;

    /// <summary>自定义检测模式</summary>
    public List<CustomInjectionPattern> CustomPatterns { get; set; } = [];
}

/// <summary>
/// 自定义注入检测模式
/// </summary>
public class CustomInjectionPattern
{
    /// <summary>正则表达式模式</summary>
    public required string Pattern { get; set; }

    /// <summary>模式分类</summary>
    public string? Category { get; set; }

    /// <summary>模式描述</summary>
    public string? Description { get; set; }

    /// <summary>严重程度</summary>
    public IssueSeverity Severity { get; set; } = IssueSeverity.Warning;
}
