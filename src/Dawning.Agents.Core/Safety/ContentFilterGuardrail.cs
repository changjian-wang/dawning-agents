using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// Content filter guardrail that detects and blocks content containing prohibited keywords.
/// </summary>
public sealed class ContentFilterGuardrail : IInputGuardrail, IOutputGuardrail
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

        // Pre-process keywords to lowercase for matching efficiency
        _blockedKeywordsLower = _options
            .BlockedKeywords.Select(k => k.ToLowerInvariant())
            .ToHashSet();
    }

    /// <inheritdoc />
    public string Name => "ContentFilterGuardrail";

    /// <inheritdoc />
    public string Description => "Detects and blocks content containing prohibited keywords";

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
            cancellationToken.ThrowIfCancellationRequested();

            var index = 0;
            while ((index = contentLower.IndexOf(keyword, index, StringComparison.Ordinal)) >= 0)
            {
                issues.Add(
                    new GuardrailIssue
                    {
                        Type = "BlockedKeyword",
                        Description = $"Blocked keyword detected",
                        Position = index,
                        Length = keyword.Length,
                        MatchedContent = MaskKeyword(keyword),
                        Severity = IssueSeverity.Error,
                    }
                );

                _logger.LogWarning("Blocked keyword detected at position {Position}", index);

                index += keyword.Length;
            }
        }

        if (issues.Count == 0)
        {
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        _logger.LogWarning("Content contains {Count} blocked keyword(s)", issues.Count);

        return Task.FromResult(
            GuardrailResult.Fail($"Content contains {issues.Count} blocked keyword(s)", Name, issues)
        );
    }

    /// <summary>
    /// Masks a keyword (shows only the first and last characters).
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
/// URL domain check guardrail that verifies URLs against allowed and blocked domain lists.
/// </summary>
public sealed class UrlDomainGuardrail : IInputGuardrail, IOutputGuardrail
{
    private static readonly Regex s_urlRegex = new(
        @"https?://([^/\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
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
    public string Description => "Checks URL domains against allowed/blocked lists";

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
            var matches = s_urlRegex.Matches(content);

            foreach (Match match in matches)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var domain = match.Groups[1].Value.ToLowerInvariant();

                // Check if on blocked list
                if (_blockedDomains.Count > 0 && IsDomainMatch(domain, _blockedDomains))
                {
                    issues.Add(
                        new GuardrailIssue
                        {
                            Type = "BlockedDomain",
                            Description = $"URL domain {domain} is on the blocked list",
                            Position = match.Index,
                            Length = match.Length,
                            MatchedContent = match.Value,
                            Severity = IssueSeverity.Error,
                        }
                    );

                    _logger.LogWarning("Blocked domain detected: {Domain}", domain);
                }
                // Check if on allowed list (if allowlist is configured)
                else if (_allowedDomains.Count > 0 && !IsDomainMatch(domain, _allowedDomains))
                {
                    issues.Add(
                        new GuardrailIssue
                        {
                            Type = "UnallowedDomain",
                            Description = $"URL domain {domain} is not on the allowed list",
                            Position = match.Index,
                            Length = match.Length,
                            MatchedContent = match.Value,
                            Severity = IssueSeverity.Warning,
                        }
                    );

                    _logger.LogWarning("Unallowed domain detected: {Domain}", domain);
                }
            }
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.LogWarning(ex, "URL regex match timed out");
            issues.Add(
                new GuardrailIssue
                {
                    Type = "RegexTimeout",
                    Description = "URL match timed out; domain check could not be completed",
                    Severity = IssueSeverity.Error,
                }
            );
        }

        if (issues.Count == 0)
        {
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        var errorCount = issues.Count(i => i.Severity == IssueSeverity.Error);

        if (errorCount > 0)
        {
            return Task.FromResult(
                GuardrailResult.Fail($"Detected {errorCount} blocked URL domain(s)", Name, issues)
            );
        }

        // Warnings only; return pass with issues
        return Task.FromResult(
            new GuardrailResult
            {
                Passed = true,
                ProcessedContent = content,
                Issues = issues,
                Message = $"Detected {issues.Count} URL domain(s) not explicitly allowed",
            }
        );
    }

    /// <summary>
    /// Checks whether a domain matches (supports subdomains).
    /// </summary>
    private static bool IsDomainMatch(string domain, HashSet<string> domains)
    {
        // Exact match
        if (domains.Contains(domain))
        {
            return true;
        }

        // Subdomain match
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
