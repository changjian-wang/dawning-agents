using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 基于 LLM 的内容审核护栏
/// </summary>
public sealed class ContentModerator : IInputGuardrail, IOutputGuardrail
{
    private readonly ILLMProvider _llmProvider;
    private readonly ContentModeratorOptions _options;
    private readonly ILogger<ContentModerator> _logger;

    public ContentModerator(
        ILLMProvider llmProvider,
        ContentModeratorOptions? options = null,
        ILogger<ContentModerator>? logger = null
    )
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _options = options ?? new ContentModeratorOptions();
        _logger = logger ?? NullLogger<ContentModerator>.Instance;
    }

    /// <inheritdoc />
    public string Name => "ContentModerator";

    /// <inheritdoc />
    public string Description => "使用 LLM 进行内容审核，检测有害、违规或不当内容";

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

    /// <inheritdoc />
    public async Task<GuardrailResult> CheckAsync(
        string content,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(content))
        {
            return GuardrailResult.Pass(content);
        }

        // 内容过长时截断（节省 Token）
        var contentToCheck =
            content.Length > _options.MaxContentToCheck
                ? content[.._options.MaxContentToCheck] + "..."
                : content;

        try
        {
            var prompt = BuildModerationPrompt(contentToCheck);

            var messages = new List<ChatMessage> { new("user", prompt) };

            var response = await _llmProvider.ChatAsync(
                messages,
                new ChatCompletionOptions
                {
                    Temperature = 0, // 确定性输出
                    MaxTokens = 200, // 审核响应不需要很长
                },
                cancellationToken
            );

            var result = ParseModerationResponse(response.Content ?? "");

            if (!result.IsAllowed)
            {
                _logger.LogWarning(
                    "内容审核未通过: Categories={Categories}, Reason={Reason}",
                    string.Join(", ", result.ViolatedCategories),
                    result.Reason
                );

                return GuardrailResult.Fail(
                    result.Reason ?? "内容审核未通过",
                    Name,
                    result
                        .ViolatedCategories.Select(c => new GuardrailIssue
                        {
                            Type = "ContentModeration",
                            Description = c,
                            Severity = IssueSeverity.Error,
                        })
                        .ToList()
                );
            }

            _logger.LogDebug("内容审核通过");
            return GuardrailResult.Pass(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "内容审核发生异常");

            // 根据配置决定异常时的行为
            if (_options.FailOpenOnError)
            {
                _logger.LogWarning("内容审核异常，根据配置允许通过");
                return GuardrailResult.Pass(content);
            }

            return GuardrailResult.Fail($"内容审核服务异常: {ex.Message}", Name);
        }
    }

    private string BuildModerationPrompt(string content)
    {
        var categories = string.Join(", ", _options.Categories);
        var jsonFormat = """
            {
              "allowed": true/false,
              "categories": ["违反的类别列表"],
              "reason": "如果不允许，说明原因"
            }
            """;

        return $"""
            你是一个内容审核助手。请分析以下内容是否违反任何审核规则。

            审核类别: {categories}

            请以 JSON 格式回复：
            {jsonFormat}

            待审核内容:
            ---
            {content}
            ---

            请只返回 JSON，不要有其他内容。
            """;
    }

    private ModerationResult ParseModerationResponse(string response)
    {
        try
        {
            // 提取 JSON 部分
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var allowed =
                    root.TryGetProperty("allowed", out var allowedProp) && allowedProp.GetBoolean();

                var categories = new List<string>();
                if (root.TryGetProperty("categories", out var categoriesProp))
                {
                    foreach (var cat in categoriesProp.EnumerateArray())
                    {
                        var catStr = cat.GetString();
                        if (!string.IsNullOrEmpty(catStr))
                        {
                            categories.Add(catStr);
                        }
                    }
                }

                var reason = root.TryGetProperty("reason", out var reasonProp)
                    ? reasonProp.GetString()
                    : null;

                return new ModerationResult
                {
                    IsAllowed = allowed,
                    ViolatedCategories = categories,
                    Reason = reason,
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析审核响应失败: {Response}", response);
        }

        // 解析失败，根据响应内容做简单判断
        var lowerResponse = response.ToLowerInvariant();
        if (
            lowerResponse.Contains("\"allowed\": false")
            || lowerResponse.Contains("\"allowed\":false")
        )
        {
            return new ModerationResult
            {
                IsAllowed = false,
                ViolatedCategories = ["Unknown"],
                Reason = "内容审核未通过",
            };
        }

        // 默认允许
        return new ModerationResult { IsAllowed = true, ViolatedCategories = [] };
    }

    private record ModerationResult
    {
        public bool IsAllowed { get; init; }
        public IReadOnlyList<string> ViolatedCategories { get; init; } = [];
        public string? Reason { get; init; }
    }
}

/// <summary>
/// 内容审核配置选项
/// </summary>
public sealed class ContentModeratorOptions
{
    /// <summary>
    /// 启用内容审核
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 审核类别
    /// </summary>
    public List<string> Categories { get; set; } =
    ["暴力内容", "色情内容", "仇恨言论", "自我伤害", "非法活动", "个人攻击", "虚假信息"];

    /// <summary>
    /// 最大检查内容长度（超过则截断）
    /// </summary>
    public int MaxContentToCheck { get; set; } = 2000;

    /// <summary>
    /// 发生错误时是否允许通过（fail-open vs fail-close）
    /// </summary>
    public bool FailOpenOnError { get; set; } = false;
}
