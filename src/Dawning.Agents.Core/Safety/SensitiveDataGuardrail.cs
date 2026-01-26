using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 敏感数据检测护栏 - 检测并脱敏敏感信息
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

        // 预编译正则表达式
        _compiledPatterns = _options
            .SensitivePatterns.Select(p => new CompiledPattern
            {
                Config = p,
                Regex = new Regex(
                    p.Pattern,
                    RegexOptions.Compiled | RegexOptions.IgnoreCase,
                    TimeSpan.FromSeconds(1)
                ),
            })
            .ToList();
    }

    /// <inheritdoc />
    public string Name => "SensitiveDataGuardrail";

    /// <inheritdoc />
    public string Description => "检测并脱敏敏感数据（邮箱、手机号、信用卡、身份证、API密钥等）";

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

        foreach (var compiled in _compiledPatterns)
        {
            try
            {
                var matches = compiled.Regex.Matches(processedContent);

                foreach (Match match in matches)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var maskedValue = MaskValue(match.Value, compiled.Config);

                    issues.Add(
                        new GuardrailIssue
                        {
                            Type = compiled.Config.Name,
                            Description = $"检测到 {compiled.Config.Name}",
                            Position = match.Index,
                            Length = match.Length,
                            MatchedContent = maskedValue, // 已脱敏的值
                            Severity = IssueSeverity.Warning,
                        }
                    );

                    _logger.LogDebug(
                        "检测到敏感数据 {PatternName} 在位置 {Position}",
                        compiled.Config.Name,
                        match.Index
                    );
                }

                // 如果配置了自动脱敏，替换原始内容
                if (_options.AutoMaskSensitiveData && matches.Count > 0)
                {
                    processedContent = compiled.Regex.Replace(
                        processedContent,
                        m => MaskValue(m.Value, compiled.Config)
                    );
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                _logger.LogWarning(ex, "正则表达式 {PatternName} 匹配超时", compiled.Config.Name);
            }
        }

        if (issues.Count == 0)
        {
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        _logger.LogInformation("检测到 {Count} 处敏感数据", issues.Count);

        // 根据配置决定行为
        if (_options.FailureBehavior == GuardrailFailureBehavior.BlockAndReport)
        {
            return Task.FromResult(
                GuardrailResult.Fail($"检测到 {issues.Count} 处敏感数据", Name, issues)
            );
        }

        // WarnAndContinue 或 SilentProcess - 返回脱敏后的内容
        return Task.FromResult(
            new GuardrailResult
            {
                Passed = true,
                ProcessedContent = processedContent,
                Issues = issues,
                Message = $"已脱敏 {issues.Count} 处敏感数据",
            }
        );
    }

    /// <summary>
    /// 脱敏值
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
