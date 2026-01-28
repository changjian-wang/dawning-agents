using System.Collections.Immutable;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 护栏管道 - 管理多个护栏的执行（线程安全）
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
        _logger.LogDebug("添加输入护栏: {GuardrailName}", guardrail.Name);
        return this;
    }

    /// <inheritdoc />
    public IGuardrailPipeline AddOutputGuardrail(IOutputGuardrail guardrail)
    {
        ArgumentNullException.ThrowIfNull(guardrail);
        ImmutableInterlocked.Update(ref _outputGuardrails, list => list.Add(guardrail));
        _logger.LogDebug("添加输出护栏: {GuardrailName}", guardrail.Name);
        return this;
    }

    /// <inheritdoc />
    public async Task<GuardrailResult> CheckInputAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        return await CheckAsync(input, _inputGuardrails, "输入", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GuardrailResult> CheckOutputAsync(
        string output,
        CancellationToken cancellationToken = default
    )
    {
        return await CheckAsync(output, _outputGuardrails, "输出", cancellationToken);
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

            _logger.LogDebug("{Phase}护栏检查: {GuardrailName}", phase, guardrail.Name);

            var result = await guardrail.CheckAsync(currentContent, cancellationToken);

            if (!result.Passed)
            {
                _logger.LogWarning(
                    "{Phase}护栏 {GuardrailName} 检查失败: {Message}",
                    phase,
                    guardrail.Name,
                    result.Message
                );

                return result;
            }

            // 收集问题
            if (result.Issues.Count > 0)
            {
                allIssues.AddRange(result.Issues);
            }

            // 使用处理后的内容继续下一个护栏
            if (!string.IsNullOrEmpty(result.ProcessedContent))
            {
                currentContent = result.ProcessedContent;
            }
        }

        _logger.LogDebug("{Phase}护栏检查全部通过，共 {Count} 个问题", phase, allIssues.Count);

        return new GuardrailResult
        {
            Passed = true,
            ProcessedContent = currentContent,
            Issues = allIssues,
        };
    }
}
