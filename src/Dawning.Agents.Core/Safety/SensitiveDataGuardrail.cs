using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// Sensitive data detection guardrail that detects and redacts sensitive information.
/// </summary>
public sealed class SensitiveDataGuardrail : IInputGuardrail, IOutputGuardrail
{
    private readonly SafetyOptions _options;
    private readonly ILogger<SensitiveDataGuardrail> _logger;
    private readonly List<CompiledPattern> _compiledPatterns;

    public SensitiveDataGuardrail(
        IOptions<SafetyOptions> options,
        ILogger<SensitiveDataGuardrail>? logger = null
    )
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<SensitiveDataGuardrail>.Instance;

        // Pre-compile regular expressions
        _compiledPatterns = _options
            .SensitivePatterns.Select(p => new CompiledPattern
            {
                Config = p,
                Regex = new Regex(
                    p.Pattern,
                    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1)
                ),
            })
            .ToList();
    }

    /// <inheritdoc />
    public string Name => "SensitiveDataGuardrail";

    /// <inheritdoc />
    public string Description => "Detects and redacts sensitive data (email, phone, credit card, ID, API keys, etc.)";

    /// <inheritdoc />
    public bool IsEnabled => _options.EnableSensitiveDataDetection;

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
        var processedContent = content;

        // First pass: detect all sensitive data on original content (ensure Position aligns with original content)
        foreach (var compiled in _compiledPatterns)
        {
            try
            {
                var matches = compiled.Regex.Matches(content);

                foreach (Match match in matches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var maskedValue = MaskValue(match.Value, compiled.Config);

                    issues.Add(
                        new GuardrailIssue
                        {
                            Type = compiled.Config.Name,
                            Description = $"Detected {compiled.Config.Name}",
                            Position = match.Index,
                            Length = match.Length,
                            MatchedContent = maskedValue, // Redacted value
                            Severity = IssueSeverity.Warning,
                        }
                    );

                    _logger.LogDebug(
                        "Sensitive data {PatternName} detected at position {Position}",
                        compiled.Config.Name,
                        match.Index
                    );
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                _logger.LogWarning(ex, "Regex pattern {PatternName} match timed out", compiled.Config.Name);
                issues.Add(
                    new GuardrailIssue
                    {
                        Type = compiled.Config.Name,
                        Description = $"Regex match timed out; {compiled.Config.Name} detection could not be completed",
                        Severity = IssueSeverity.Error,
                    }
                );
            }
        }

        // Second pass: if auto-redaction is configured, perform replacement on processedContent
        if (_options.AutoMaskSensitiveData && issues.Count > 0)
        {
            foreach (var compiled in _compiledPatterns)
            {
                processedContent = compiled.Regex.Replace(
                    processedContent,
                    m => MaskValue(m.Value, compiled.Config)
                );
            }
        }

        if (issues.Count == 0)
        {
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        _logger.LogInformation("Detected {Count} instance(s) of sensitive data", issues.Count);

        // Decide behavior based on configuration
        if (_options.FailureBehavior == GuardrailFailureBehavior.BlockAndReport)
        {
            return Task.FromResult(
                GuardrailResult.Fail($"Detected {issues.Count} instance(s) of sensitive data", Name, issues)
            );
        }

        // WarnAndContinue or SilentProcess - return redacted content
        return Task.FromResult(
            new GuardrailResult
            {
                Passed = true,
                ProcessedContent = processedContent,
                Issues = issues,
                Message = $"Redacted {issues.Count} instance(s) of sensitive data",
            }
        );
    }

    /// <summary>
    /// Redacts the given value.
    /// </summary>
    private static string MaskValue(string value, SensitivePattern config)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var keepFirst = Math.Min(config.KeepFirst, value.Length);
        var keepLast = Math.Min(config.KeepLast, value.Length - keepFirst);
        var maskLength = value.Length - keepFirst - keepLast;

        if (maskLength <= 0)
        {
            return value;
        }

        var prefix = value[..keepFirst];
        var suffix = keepLast > 0 ? value[^keepLast..] : "";
        var mask = new string(config.MaskChar, maskLength);

        return $"{prefix}{mask}{suffix}";
    }

    private class CompiledPattern
    {
        public required SensitivePattern Config { get; init; }
        public required Regex Regex { get; init; }
    }
}
