using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 长度限制护栏 - 检查输入/输出的长度
/// </summary>
public class MaxLengthGuardrail : IInputGuardrail, IOutputGuardrail
{
    private readonly int _maxLength;
    private readonly bool _isInputGuardrail;
    private readonly ILogger<MaxLengthGuardrail> _logger;

    /// <summary>
    /// 创建输入长度护栏
    /// </summary>
    public static MaxLengthGuardrail ForInput(
        IOptions<SafetyOptions> options,
        ILogger<MaxLengthGuardrail>? logger = null
    ) => new(options.Value.MaxInputLength, true, logger);

    /// <summary>
    /// 创建输出长度护栏
    /// </summary>
    public static MaxLengthGuardrail ForOutput(
        IOptions<SafetyOptions> options,
        ILogger<MaxLengthGuardrail>? logger = null
    ) => new(options.Value.MaxOutputLength, false, logger);

    /// <summary>
    /// 创建自定义长度护栏
    /// </summary>
    public MaxLengthGuardrail(
        int maxLength,
        bool isInputGuardrail = true,
        ILogger<MaxLengthGuardrail>? logger = null
    )
    {
        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxLength),
                "Max length must be greater than 0"
            );
        }

        _maxLength = maxLength;
        _isInputGuardrail = isInputGuardrail;
        _logger = logger ?? NullLogger<MaxLengthGuardrail>.Instance;
    }

    /// <inheritdoc />
    public string Name => _isInputGuardrail ? "MaxInputLength" : "MaxOutputLength";

    /// <inheritdoc />
    public string Description =>
        _isInputGuardrail
            ? $"限制输入长度不超过 {_maxLength} 字符"
            : $"限制输出长度不超过 {_maxLength} 字符";

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public Task<GuardrailResult> CheckAsync(
        string content,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(content))
        {
            return Task.FromResult(GuardrailResult.Pass());
        }

        var length = content.Length;

        if (length <= _maxLength)
        {
            _logger.LogDebug(
                "{GuardrailName}: 内容长度 {Length} 在限制 {MaxLength} 内",
                Name,
                length,
                _maxLength
            );
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        var issue = new GuardrailIssue
        {
            Type = "LengthExceeded",
            Description = $"内容长度 {length} 超过最大限制 {_maxLength}",
            Severity = IssueSeverity.Error,
        };

        _logger.LogWarning(
            "{GuardrailName}: 内容长度 {Length} 超过限制 {MaxLength}",
            Name,
            length,
            _maxLength
        );

        return Task.FromResult(
            GuardrailResult.Fail($"内容长度 ({length}) 超过最大限制 ({_maxLength})", Name, [issue])
        );
    }
}
