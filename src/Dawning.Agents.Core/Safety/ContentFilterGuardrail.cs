using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 内容过滤护栏 - 检测并阻止包含禁用关键词的内容
/// </summary>
public class ContentFilterGuardrail : IInputGuardrail, IOutputGuardrail
{
    private readonly SafetyOptions _options;
    private readonly ILogger<ContentFilterGuardrail> _logger;
    private readonly HashSet<string> _blockedKeywordsLower;

    public ContentFilterGuardrail(
        IOptions<SafetyOptions> options,
        ILogger<ContentFilterGuardrail>? logger = null
    )
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<ContentFilterGuardrail>.Instance;

        // 预处理关键词为小写，提高匹配效率
        _blockedKeywordsLower = _options
            .BlockedKeywords.Select(k => k.ToLowerInvariant())
            .ToHashSet();
    }

    /// <inheritdoc />
    public string Name => "ContentFilterGuardrail";

    /// <inheritdoc />
    public string Description => "检测并阻止包含禁用关键词的内容";

    /// <inheritdoc />
    public bool IsEnabled => _options.EnableContentFilter && _blockedKeywordsLower.Count > 0;

    /// <inheritdoc />
    public Task<GuardrailResult> CheckAsync(
        string content,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled || string.IsNullOrEmpty(content))
        {
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        var contentLower = content.ToLowerInvariant();
        var issues = new List<GuardrailIssue>();

        foreach (var keyword in _blockedKeywordsLower)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var index = 0;
            while ((index = contentLower.IndexOf(keyword, index, StringComparison.Ordinal)) >= 0)
            {
                issues.Add(
                    new GuardrailIssue
                    {
                        Type = "BlockedKeyword",
                        Description = $"检测到禁用关键词",
                        Position = index,
                        Length = keyword.Length,
                        MatchedContent = MaskKeyword(keyword),
                        Severity = IssueSeverity.Error,
                    }
                );

                _logger.LogWarning("检测到禁用关键词在位置 {Position}", index);

                index += keyword.Length;
            }
        }

        if (issues.Count == 0)
        {
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        _logger.LogWarning("内容包含 {Count} 个禁用关键词", issues.Count);

        return Task.FromResult(
            GuardrailResult.Fail($"内容包含 {issues.Count} 个禁用关键词", Name, issues)
        );
    }

    /// <summary>
    /// 遮蔽关键词（只显示首尾字符）
    /// </summary>
    private static string MaskKeyword(string keyword)
    {
        if (keyword.Length <= 2)
        {
            return new string('*', keyword.Length);
        }

        return $"{keyword[0]}{new string('*', keyword.Length - 2)}{keyword[^1]}";
    }
}

/// <summary>
/// URL 域名检查护栏 - 检查 URL 是否在允许列表中
/// </summary>
public class UrlDomainGuardrail : IInputGuardrail, IOutputGuardrail
{
    private static readonly Regex UrlRegex = new(
        @"https?://([^/\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1)
    );

    private readonly SafetyOptions _options;
    private readonly ILogger<UrlDomainGuardrail> _logger;
    private readonly HashSet<string> _allowedDomains;
    private readonly HashSet<string> _blockedDomains;

    public UrlDomainGuardrail(
        IOptions<SafetyOptions> options,
        ILogger<UrlDomainGuardrail>? logger = null
    )
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<UrlDomainGuardrail>.Instance;

        _allowedDomains = _options.AllowedDomains.Select(d => d.ToLowerInvariant()).ToHashSet();
        _blockedDomains = _options.BlockedDomains.Select(d => d.ToLowerInvariant()).ToHashSet();
    }

    /// <inheritdoc />
    public string Name => "UrlDomainGuardrail";

    /// <inheritdoc />
    public string Description => "检查 URL 域名是否在允许/阻止列表中";

    /// <inheritdoc />
    public bool IsEnabled => _allowedDomains.Count > 0 || _blockedDomains.Count > 0;

    /// <inheritdoc />
    public Task<GuardrailResult> CheckAsync(
        string content,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled || string.IsNullOrEmpty(content))
        {
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        var issues = new List<GuardrailIssue>();

        try
        {
            var matches = UrlRegex.Matches(content);

            foreach (Match match in matches)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var domain = match.Groups[1].Value.ToLowerInvariant();

                // 检查是否在阻止列表
                if (_blockedDomains.Count > 0 && IsDomainMatch(domain, _blockedDomains))
                {
                    issues.Add(
                        new GuardrailIssue
                        {
                            Type = "BlockedDomain",
                            Description = $"URL 域名 {domain} 在阻止列表中",
                            Position = match.Index,
                            Length = match.Length,
                            MatchedContent = match.Value,
                            Severity = IssueSeverity.Error,
                        }
                    );

                    _logger.LogWarning("检测到阻止的域名: {Domain}", domain);
                }
                // 检查是否在允许列表（如果配置了白名单）
                else if (_allowedDomains.Count > 0 && !IsDomainMatch(domain, _allowedDomains))
                {
                    issues.Add(
                        new GuardrailIssue
                        {
                            Type = "UnallowedDomain",
                            Description = $"URL 域名 {domain} 不在允许列表中",
                            Position = match.Index,
                            Length = match.Length,
                            MatchedContent = match.Value,
                            Severity = IssueSeverity.Warning,
                        }
                    );

                    _logger.LogWarning("检测到未允许的域名: {Domain}", domain);
                }
            }
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.LogWarning(ex, "URL 正则匹配超时");
        }

        if (issues.Count == 0)
        {
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        var errorCount = issues.Count(i => i.Severity == IssueSeverity.Error);

        if (errorCount > 0)
        {
            return Task.FromResult(
                GuardrailResult.Fail($"检测到 {errorCount} 个阻止的 URL 域名", Name, issues)
            );
        }

        // 只有警告，返回通过但带问题
        return Task.FromResult(
            new GuardrailResult
            {
                Passed = true,
                ProcessedContent = content,
                Issues = issues,
                Message = $"检测到 {issues.Count} 个未明确允许的 URL 域名",
            }
        );
    }

    /// <summary>
    /// 检查域名是否匹配（支持子域名）
    /// </summary>
    private static bool IsDomainMatch(string domain, HashSet<string> domains)
    {
        // 精确匹配
        if (domains.Contains(domain))
        {
            return true;
        }

        // 子域名匹配
        foreach (var d in domains)
        {
            if (domain.EndsWith($".{d}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
