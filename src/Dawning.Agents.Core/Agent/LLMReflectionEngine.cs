using System.Text.Json;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Agent;

/// <summary>
/// 基于 LLM 的反思引擎 — 通过调用 LLM 诊断失败并建议修复策略
/// </summary>
public sealed class LLMReflectionEngine : IReflectionEngine
{
    private readonly ILLMProvider _llmProvider;
    private readonly ILogger<LLMReflectionEngine> _logger;

    /// <summary>
    /// 创建 LLM 反思引擎
    /// </summary>
    public LLMReflectionEngine(
        ILLMProvider llmProvider,
        IOptions<ReflectionOptions> options,
        ILogger<LLMReflectionEngine>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(llmProvider);
        ArgumentNullException.ThrowIfNull(options);

        _llmProvider = llmProvider;
        _logger = logger ?? NullLogger<LLMReflectionEngine>.Instance;
    }

    /// <inheritdoc />
    public async Task<ReflectionResult> ReflectAsync(
        ReflectionContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "Reflecting on failed tool '{ToolName}' with error: {Error}",
            context.FailedTool.Name,
            context.FailedResult.Error
        );

        var prompt = BuildReflectionPrompt(context);
        var messages = new List<ChatMessage>
        {
            new(
                "system",
                "You are a diagnostic engine that analyzes tool failures and suggests repair strategies. "
                    + "Respond in JSON format with fields: action (Retry|ReviseAndRetry|Abandon|CreateNew|Escalate), "
                    + "diagnosis (string), confidence (float 0-1), and optionally revisedScript (string) when action is ReviseAndRetry."
            ),
            new("user", prompt),
        };

        var chatOptions = new ChatCompletionOptions { Temperature = 0.2f };

        try
        {
            var response = await _llmProvider
                .ChatAsync(messages, chatOptions, cancellationToken)
                .ConfigureAwait(false);

            return ParseReflectionResponse(response.Content, context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Reflection failed for tool '{ToolName}', defaulting to Retry",
                context.FailedTool.Name
            );

            return new ReflectionResult
            {
                Action = ReflectionAction.Retry,
                Diagnosis = $"Reflection engine error: {ex.Message}",
                Confidence = 0.1f,
            };
        }
    }

    private static string BuildReflectionPrompt(ReflectionContext context)
    {
        var parts = new List<string>
        {
            $"## Failed Tool: {context.FailedTool.Name}",
            $"Description: {context.FailedTool.Description}",
            $"## Task: {context.TaskDescription}",
            $"## Input: {context.Input}",
            $"## Error: {context.FailedResult.Error ?? "(no error message)"}",
        };

        if (context.FailedTool is EphemeralTool ephemeral)
        {
            parts.Add($"## Script:\n```\n{ephemeral.Definition.Script}\n```");
            parts.Add($"## Runtime: {ephemeral.Definition.Runtime}");

            if (ephemeral.Definition.Metadata.FailurePatterns.Count > 0)
            {
                parts.Add(
                    "## Known Failure Patterns:\n"
                        + string.Join(
                            "\n",
                            ephemeral.Definition.Metadata.FailurePatterns.Select(p => $"- {p}")
                        )
                );
            }
        }

        if (context.UsageStats is not null)
        {
            parts.Add(
                $"## Usage Stats: {context.UsageStats.TotalCalls} calls, "
                    + $"{context.UsageStats.SuccessRate:P0} success rate"
            );
        }

        parts.Add(
            "\nAnalyze the failure and respond with a JSON object containing: "
                + "action, diagnosis, confidence, and revisedScript (if action is ReviseAndRetry)."
        );

        return string.Join("\n", parts);
    }

    private ReflectionResult ParseReflectionResponse(string content, ReflectionContext context)
    {
        try
        {
            // 提取 JSON 块（LLM 可能添加 markdown 代码块）
            var jsonContent = ExtractJsonBlock(content);
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            var actionStr = root.TryGetProperty("action", out var actionProp)
                ? actionProp.GetString()
                : "Retry";

            var action = Enum.TryParse<ReflectionAction>(actionStr, ignoreCase: true, out var a)
                ? a
                : ReflectionAction.Retry;

            var diagnosis = root.TryGetProperty("diagnosis", out var diagProp)
                ? diagProp.GetString()
                : null;

            var confidence = root.TryGetProperty("confidence", out var confProp)
                ? confProp.GetSingle()
                : 0.5f;

            EphemeralToolDefinition? revisedDef = null;
            if (
                action == ReflectionAction.ReviseAndRetry
                && root.TryGetProperty("revisedScript", out var scriptProp)
                && context.FailedTool is EphemeralTool failedEphemeral
            )
            {
                var revisedScript = scriptProp.GetString();
                if (revisedScript != null)
                {
                    revisedDef = new EphemeralToolDefinition
                    {
                        Name = failedEphemeral.Definition.Name,
                        Description = failedEphemeral.Definition.Description,
                        Runtime = failedEphemeral.Definition.Runtime,
                        Script = revisedScript,
                        Parameters = failedEphemeral.Definition.Parameters,
                        Scope = failedEphemeral.Definition.Scope,
                        Metadata = new EphemeralToolMetadata
                        {
                            Author = failedEphemeral.Definition.Metadata.Author,
                            Tags = failedEphemeral.Definition.Metadata.Tags,
                            WhenToUse = failedEphemeral.Definition.Metadata.WhenToUse,
                            Limitations = failedEphemeral.Definition.Metadata.Limitations,
                            FailurePatterns =
                            [
                                .. failedEphemeral.Definition.Metadata.FailurePatterns,
                                context.FailedResult.Error ?? "unknown failure",
                            ],
                            RelatedSkills = failedEphemeral.Definition.Metadata.RelatedSkills,
                            Version = failedEphemeral.Definition.Metadata.Version + 1,
                            RevisionCount = failedEphemeral.Definition.Metadata.RevisionCount + 1,
                            LastRevisedAt = DateTimeOffset.UtcNow,
                        },
                    };
                }
            }

            return new ReflectionResult
            {
                Action = action,
                Diagnosis = diagnosis,
                Confidence = confidence,
                RevisedDefinition = revisedDef,
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse reflection response as JSON");
            return new ReflectionResult
            {
                Action = ReflectionAction.Retry,
                Diagnosis = $"Could not parse reflection: {content}",
                Confidence = 0.2f,
            };
        }
    }

    private static string ExtractJsonBlock(string content)
    {
        // 尝试提取 ```json ... ``` 格式
        var jsonStart = content.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonStart >= 0)
        {
            jsonStart = content.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = content.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (jsonEnd > jsonStart)
            {
                return content[jsonStart..jsonEnd].Trim();
            }
        }

        // 尝试找到 { ... } 块
        var braceStart = content.IndexOf('{');
        var braceEnd = content.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
        {
            return content[braceStart..(braceEnd + 1)];
        }

        return content;
    }
}
