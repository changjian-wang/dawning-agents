using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// Length limit guardrail that checks the length of input or output.
/// </summary>
public sealed class MaxLengthGuardrail : IInputGuardrail, IOutputGuardrail
{
    private readonly int _maxLength;
    private readonly bool _isInputGuardrail;
    private readonly ILogger<MaxLengthGuardrail> _logger;

    /// <summary>
    /// Creates an input length guardrail.
    /// </summary>
    public static MaxLengthGuardrail ForInput(
        IOptions<SafetyOptions> options,
        ILogger<MaxLengthGuardrail>? logger = null
    ) => new(options.Value.MaxInputLength, true, logger);

    /// <summary>
    /// Creates an output length guardrail.
    /// </summary>
    public static MaxLengthGuardrail ForOutput(
        IOptions<SafetyOptions> options,
        ILogger<MaxLengthGuardrail>? logger = null
    ) => new(options.Value.MaxOutputLength, false, logger);

    /// <summary>
    /// Creates a custom length guardrail.
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
? $"Limits input length to a maximum of {_maxLength} characters"
        : $"Limits output length to a maximum of {_maxLength} characters";

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
                "{GuardrailName}: Content length {Length} is within limit {MaxLength}",
                Name,
                length,
                _maxLength
            );
            return Task.FromResult(GuardrailResult.Pass(content));
        }

        var issue = new GuardrailIssue
        {
            Type = "LengthExceeded",
            Description = $"Content length {length} exceeds maximum limit {_maxLength}",
            Severity = IssueSeverity.Error,
        };

        _logger.LogWarning(
            "{GuardrailName}: Content length {Length} exceeds limit {MaxLength}",
            Name,
            length,
            _maxLength
        );

        return Task.FromResult(
            GuardrailResult.Fail($"Content length ({length}) exceeds maximum limit ({_maxLength})", Name, [issue])
        );
    }
}
