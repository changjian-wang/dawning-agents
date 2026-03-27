using System.Security.Cryptography;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// LLM-based content moderation guardrail.
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
    public string Description => "Uses an LLM for content moderation to detect harmful, violating, or inappropriate content";

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

        // Truncate content if too long (to save tokens)
        var contentToCheck =
            content.Length > _options.MaxContentToCheck
                ? content[.._options.MaxContentToCheck] + "..."
                : content;

        try
        {
            var prompt = BuildModerationPrompt(contentToCheck);

            var messages = new List<ChatMessage> { new("user", prompt) };

            var response = await _llmProvider
                .ChatAsync(
                    messages,
                    new ChatCompletionOptions
                    {
                        Temperature = 0, // Deterministic output
                        MaxTokens = 200, // Moderation response does not need to be long
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);

            var result = ParseModerationResponse(response.Content ?? "");

            if (!result.IsAllowed)
            {
                _logger.LogWarning(
                    "Content moderation failed: Categories={Categories}, Reason={Reason}",
                    string.Join(", ", result.ViolatedCategories),
                    result.Reason
                );

                return GuardrailResult.Fail(
                    result.Reason ?? "Content moderation failed",
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

            _logger.LogDebug("Content moderation passed");
            return GuardrailResult.Pass(content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Content moderation encountered an exception");

            // Decide behavior on exception based on configuration
            if (_options.FailOpenOnError)
            {
                _logger.LogWarning("Content moderation exception; allowing through per configuration");
                return GuardrailResult.Pass(content);
            }

            return GuardrailResult.Fail($"Content moderation service error: {ex.Message}", Name);
        }
    }

    private string BuildModerationPrompt(string content)
    {
        var categories = string.Join(", ", _options.Categories);
        var boundary = $"BOUNDARY_{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";
        var jsonFormat = """
            {
              "allowed": true/false,
              "categories": ["list of violated categories"],
              "reason": "if not allowed, explain why"
            }
            """;

        return $"""
            You are a content moderation assistant. Analyze the following content for any moderation rule violations.

            Moderation categories: {categories}

            Reply in JSON format:
            {jsonFormat}

            Content to moderate:
            {boundary}
            {content}
            {boundary}

            Return only JSON, nothing else.
            """;
    }

    private ModerationResult ParseModerationResponse(string response)
    {
        try
        {
            // Extract JSON portion
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var allowed = false;
                if (root.TryGetProperty("allowed", out var allowedProp))
                {
                    allowed = allowedProp.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.True => true,
                        System.Text.Json.JsonValueKind.False => false,
                        System.Text.Json.JsonValueKind.String => bool.TryParse(
                            allowedProp.GetString(),
                            out var parsed
                        ) && parsed,
                        _ => false,
                    };
                }

                var categories = new List<string>();
                if (root.TryGetProperty("categories", out var categoriesProp))
                {
                    if (categoriesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
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
                    else if (categoriesProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var singleCategory = categoriesProp.GetString();
                        if (!string.IsNullOrEmpty(singleCategory))
                        {
                            categories.Add(singleCategory);
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
            _logger.LogWarning(ex, "Failed to parse moderation response: {Response}", response);
        }

        // Parsing failed; make a simple judgment based on response content
        var lowerResponse = response.ToLowerInvariant();
        if (
            lowerResponse.Contains("\"allowed\": false", StringComparison.Ordinal)
            || lowerResponse.Contains("\"allowed\":false", StringComparison.Ordinal)
        )
        {
            return new ModerationResult
            {
                IsAllowed = false,
                ViolatedCategories = ["Unknown"],
                Reason = "Content moderation failed",
            };
        }

        // Default deny (fail-closed: deny when safety cannot be confirmed)
        return new ModerationResult
        {
            IsAllowed = false,
            ViolatedCategories = ["ParseError"],
            Reason = "Unable to parse moderation response; denied by default",
        };
    }

    private record ModerationResult
    {
        public bool IsAllowed { get; init; }
        public IReadOnlyList<string> ViolatedCategories { get; init; } = [];
        public string? Reason { get; init; }
    }
}

/// <summary>
/// Content moderation configuration options.
/// </summary>
public sealed class ContentModeratorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether content moderation is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the moderation categories.
    /// </summary>
    public List<string> Categories { get; set; } =
    ["Violence", "Sexual content", "Hate speech", "Self-harm", "Illegal activity", "Personal attacks", "Misinformation"];

    /// <summary>
    /// Gets or sets the maximum content length to check (content beyond this is truncated).
    /// </summary>
    public int MaxContentToCheck { get; set; } = 2000;

    /// <summary>
    /// Gets or sets a value indicating whether to allow content through on error (fail-open vs fail-closed).
    /// </summary>
    public bool FailOpenOnError { get; set; } = false;
}
