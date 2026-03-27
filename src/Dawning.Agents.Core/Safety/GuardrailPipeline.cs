using System.Collections.Immutable;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// Guardrail pipeline that manages the execution of multiple guardrails (thread-safe).
/// </summary>
public sealed class GuardrailPipeline : IGuardrailPipeline
{
    private ImmutableList<IInputGuardrail> _inputGuardrails = ImmutableList<IInputGuardrail>.Empty;
    private ImmutableList<IOutputGuardrail> _outputGuardrails =
        ImmutableList<IOutputGuardrail>.Empty;
    private readonly ILogger<GuardrailPipeline> _logger;

    public GuardrailPipeline(ILogger<GuardrailPipeline>? logger = null)
    {
        _logger = logger ?? NullLogger<GuardrailPipeline>.Instance;
    }

    /// <inheritdoc />
    public IReadOnlyList<IInputGuardrail> InputGuardrails => _inputGuardrails;

    /// <inheritdoc />
    public IReadOnlyList<IOutputGuardrail> OutputGuardrails => _outputGuardrails;

    /// <inheritdoc />
    public IGuardrailPipeline AddInputGuardrail(IInputGuardrail guardrail)
    {
        ArgumentNullException.ThrowIfNull(guardrail);
        ImmutableInterlocked.Update(ref _inputGuardrails, list => list.Add(guardrail));
        _logger.LogDebug("Input guardrail added: {GuardrailName}", guardrail.Name);
        return this;
    }

    /// <inheritdoc />
    public IGuardrailPipeline AddOutputGuardrail(IOutputGuardrail guardrail)
    {
        ArgumentNullException.ThrowIfNull(guardrail);
        ImmutableInterlocked.Update(ref _outputGuardrails, list => list.Add(guardrail));
        _logger.LogDebug("Output guardrail added: {GuardrailName}", guardrail.Name);
        return this;
    }

    /// <inheritdoc />
    public async Task<GuardrailResult> CheckInputAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        return await CheckAsync(input, _inputGuardrails, "Input", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GuardrailResult> CheckOutputAsync(
        string output,
        CancellationToken cancellationToken = default
    )
    {
        return await CheckAsync(output, _outputGuardrails, "Output", cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<GuardrailResult> CheckAsync<T>(
        string content,
        IReadOnlyList<T> guardrails,
        string phase,
        CancellationToken cancellationToken
    )
        where T : IGuardrail
    {
        if (guardrails.Count == 0)
        {
            return GuardrailResult.Pass(content);
        }

        var currentContent = content;
        var allIssues = new List<GuardrailIssue>();

        foreach (var guardrail in guardrails)
        {
            if (!guardrail.IsEnabled)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug("{Phase} guardrail check: {GuardrailName}", phase, guardrail.Name);

            var result = await guardrail
                .CheckAsync(currentContent, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Passed)
            {
                _logger.LogWarning(
                    "{Phase} guardrail {GuardrailName} check failed: {Message}",
                    phase,
                    guardrail.Name,
                    result.Message
                );

                return result;
            }

            // Collect issues
            if (result.Issues.Count > 0)
            {
                allIssues.AddRange(result.Issues);
            }

            // Use processed content for the next guardrail
            if (result.ProcessedContent != null)
            {
                currentContent = result.ProcessedContent;
            }
        }

        _logger.LogDebug(
            "{Phase} guardrail checks all passed, {Count} issue(s) total",
            phase,
            allIssues.Count
        );

        return new GuardrailResult
        {
            Passed = true,
            ProcessedContent = currentContent,
            Issues = allIssues,
        };
    }
}
