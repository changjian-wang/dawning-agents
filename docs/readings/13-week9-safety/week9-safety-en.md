# Week 9: Safety & Guardrails

> Phase 5: Advanced Topics
> Week 9 Learning Material: Input/Output Validation, Content Filtering & Safety Patterns

---

## Day 1-2: Safety Fundamentals

### 1. Why Safety Matters

```text
┌─────────────────────────────────────────────────────────────────┐
│                    Agent Safety Concerns                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │   Prompt       │    │   Harmful      │    │   Resource     │ │
│  │   Injection    │    │   Content      │    │   Abuse        │ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │   Data         │    │   Unintended   │    │   Infinite     │ │
│  │   Leakage      │    │   Actions      │    │   Loops        │ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Safety Configuration

```csharp
namespace DawningAgents.Core.Safety;

/// <summary>
/// Safety configuration for agents
/// </summary>
public record SafetyConfig
{
    /// <summary>
    /// Maximum iterations per request
    /// </summary>
    public int MaxIterations { get; init; } = 10;
    
    /// <summary>
    /// Maximum tokens per response
    /// </summary>
    public int MaxTokensPerResponse { get; init; } = 4000;
    
    /// <summary>
    /// Maximum total tokens per session
    /// </summary>
    public int MaxTokensPerSession { get; init; } = 100000;
    
    /// <summary>
    /// Request timeout
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Enable input validation
    /// </summary>
    public bool EnableInputValidation { get; init; } = true;
    
    /// <summary>
    /// Enable output filtering
    /// </summary>
    public bool EnableOutputFiltering { get; init; } = true;
    
    /// <summary>
    /// Enable content moderation
    /// </summary>
    public bool EnableContentModeration { get; init; } = true;
    
    /// <summary>
    /// Blocked topics
    /// </summary>
    public IReadOnlyList<string> BlockedTopics { get; init; } = [];
    
    /// <summary>
    /// Allowed domains for web requests
    /// </summary>
    public IReadOnlyList<string> AllowedDomains { get; init; } = [];
    
    /// <summary>
    /// Sensitive data patterns to redact
    /// </summary>
    public IReadOnlyList<string> SensitivePatterns { get; init; } = [
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",  // Email
        @"\b\d{3}-\d{2}-\d{4}\b",  // SSN
        @"\b\d{16}\b",  // Credit card
        @"(?i)(password|secret|api[_-]?key|token)\s*[:=]\s*\S+"  // Credentials
    ];
}
```

### 3. Guardrail Interface

```csharp
namespace DawningAgents.Core.Safety;

/// <summary>
/// Interface for safety guardrails
/// </summary>
public interface IGuardrail
{
    /// <summary>
    /// Guardrail name
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Priority (lower = earlier execution)
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Check input before processing
    /// </summary>
    Task<GuardrailResult> CheckInputAsync(
        string input,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check output before returning
    /// </summary>
    Task<GuardrailResult> CheckOutputAsync(
        string output,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from guardrail check
/// </summary>
public record GuardrailResult
{
    public bool IsAllowed { get; init; }
    public string? BlockReason { get; init; }
    public string? ModifiedContent { get; init; }
    public GuardrailAction Action { get; init; }
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    public static GuardrailResult Allow() => new() { IsAllowed = true, Action = GuardrailAction.Allow };
    
    public static GuardrailResult Block(string reason) => new()
    {
        IsAllowed = false,
        BlockReason = reason,
        Action = GuardrailAction.Block
    };
    
    public static GuardrailResult Modify(string modifiedContent) => new()
    {
        IsAllowed = true,
        ModifiedContent = modifiedContent,
        Action = GuardrailAction.Modify
    };
    
    public static GuardrailResult Warn(string reason) => new()
    {
        IsAllowed = true,
        BlockReason = reason,
        Action = GuardrailAction.Warn
    };
}

public enum GuardrailAction
{
    Allow,
    Block,
    Modify,
    Warn
}
```

---

## Day 3-4: Input Validation & Filtering

### 1. Input Validator

```csharp
namespace DawningAgents.Core.Safety.Guardrails;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Validates and sanitizes input
/// </summary>
public class InputValidator : IGuardrail
{
    private readonly SafetyConfig _config;
    private readonly ILogger<InputValidator> _logger;
    private readonly List<Regex> _blockedPatterns;

    public string Name => "InputValidator";
    public int Priority => 0;

    public InputValidator(SafetyConfig config, ILogger<InputValidator> logger)
    {
        _config = config;
        _logger = logger;
        _blockedPatterns = InitializeBlockedPatterns();
    }

    public Task<GuardrailResult> CheckInputAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Task.FromResult(GuardrailResult.Block("Empty input"));
        }

        // Check length
        if (input.Length > 10000)
        {
            return Task.FromResult(GuardrailResult.Block("Input too long"));
        }

        // Check for prompt injection patterns
        var injectionResult = CheckPromptInjection(input);
        if (!injectionResult.IsAllowed)
        {
            _logger.LogWarning("Prompt injection detected: {Reason}", injectionResult.BlockReason);
            return Task.FromResult(injectionResult);
        }

        // Check blocked topics
        foreach (var topic in _config.BlockedTopics)
        {
            if (input.Contains(topic, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Blocked topic detected: {Topic}", topic);
                return Task.FromResult(GuardrailResult.Block($"Topic not allowed: {topic}"));
            }
        }

        // Check blocked patterns
        foreach (var pattern in _blockedPatterns)
        {
            if (pattern.IsMatch(input))
            {
                _logger.LogWarning("Blocked pattern matched");
                return Task.FromResult(GuardrailResult.Block("Content contains blocked patterns"));
            }
        }

        return Task.FromResult(GuardrailResult.Allow());
    }

    public Task<GuardrailResult> CheckOutputAsync(
        string output,
        CancellationToken cancellationToken = default)
    {
        // Input validator doesn't check output
        return Task.FromResult(GuardrailResult.Allow());
    }

    private GuardrailResult CheckPromptInjection(string input)
    {
        var injectionPatterns = new[]
        {
            @"ignore\s+(previous|above|all)\s+(instructions?|prompts?)",
            @"disregard\s+(previous|above|all)",
            @"forget\s+(everything|all|previous)",
            @"you\s+are\s+now\s+",
            @"new\s+instructions?:",
            @"system\s*:\s*",
            @"\[INST\]",
            @"<\|im_start\|>",
            @"###\s*(System|Human|Assistant)",
        };

        foreach (var pattern in injectionPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                return GuardrailResult.Block("Potential prompt injection detected");
            }
        }

        return GuardrailResult.Allow();
    }

    private List<Regex> InitializeBlockedPatterns()
    {
        var patterns = new List<Regex>();
        
        // Add default harmful patterns
        var defaultPatterns = new[]
        {
            @"(?i)(how\s+to\s+)?(make|create|build)\s+(a\s+)?(bomb|explosive|weapon)",
            @"(?i)(hack|exploit|attack)\s+(into|the)",
        };

        foreach (var pattern in defaultPatterns)
        {
            patterns.Add(new Regex(pattern, RegexOptions.Compiled));
        }

        return patterns;
    }
}
```

### 2. Content Moderator

```csharp
namespace DawningAgents.Core.Safety.Guardrails;

using Microsoft.Extensions.Logging;

/// <summary>
/// Moderates content using LLM
/// </summary>
public class ContentModerator : IGuardrail
{
    private readonly ILLMProvider _llm;
    private readonly ILogger<ContentModerator> _logger;
    private readonly string[] _categories;

    public string Name => "ContentModerator";
    public int Priority => 10;

    public ContentModerator(
        ILLMProvider llm,
        ILogger<ContentModerator> logger,
        string[]? categories = null)
    {
        _llm = llm;
        _logger = logger;
        _categories = categories ?? [
            "violence",
            "harassment",
            "hate_speech",
            "self_harm",
            "sexual_content",
            "illegal_activity"
        ];
    }

    public async Task<GuardrailResult> CheckInputAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return await ModerateContentAsync(input, "input", cancellationToken);
    }

    public async Task<GuardrailResult> CheckOutputAsync(
        string output,
        CancellationToken cancellationToken = default)
    {
        return await ModerateContentAsync(output, "output", cancellationToken);
    }

    private async Task<GuardrailResult> ModerateContentAsync(
        string content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var prompt = $"""
            You are a content moderator. Analyze the following {contentType} and determine if it violates any of these categories:
            {string.Join(", ", _categories)}

            Content to analyze:
            {content}

            Respond with a JSON object:
            {{
                "is_safe": true/false,
                "violated_categories": ["category1", "category2"],
                "confidence": 0.0-1.0,
                "reason": "explanation if not safe"
            }}
            """;

        try
        {
            var response = await _llm.GenerateAsync(prompt, cancellationToken: cancellationToken);
            var result = ParseModerationResult(response.Content);

            if (!result.IsSafe)
            {
                _logger.LogWarning("Content moderation failed: {Reason}", result.Reason);
                return GuardrailResult.Block(result.Reason ?? "Content violates safety policies");
            }

            return GuardrailResult.Allow();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Content moderation error");
            // Fail open or closed based on configuration
            return GuardrailResult.Warn("Moderation check failed");
        }
    }

    private ModerationResult ParseModerationResult(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..jsonEnd];
                return JsonSerializer.Deserialize<ModerationResult>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new ModerationResult { IsSafe = true };
            }
        }
        catch
        {
            // Parsing failed
        }

        return new ModerationResult { IsSafe = true };
    }

    private record ModerationResult
    {
        public bool IsSafe { get; init; }
        public string[]? ViolatedCategories { get; init; }
        public double Confidence { get; init; }
        public string? Reason { get; init; }
    }
}
```

### 3. Sensitive Data Filter

```csharp
namespace DawningAgents.Core.Safety.Guardrails;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Filters and redacts sensitive data
/// </summary>
public class SensitiveDataFilter : IGuardrail
{
    private readonly SafetyConfig _config;
    private readonly ILogger<SensitiveDataFilter> _logger;
    private readonly List<(Regex Pattern, string Replacement)> _redactionPatterns;

    public string Name => "SensitiveDataFilter";
    public int Priority => 20;

    public SensitiveDataFilter(SafetyConfig config, ILogger<SensitiveDataFilter> logger)
    {
        _config = config;
        _logger = logger;
        _redactionPatterns = BuildRedactionPatterns();
    }

    public Task<GuardrailResult> CheckInputAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var (modified, redactionCount) = RedactSensitiveData(input);
        
        if (redactionCount > 0)
        {
            _logger.LogInformation("Redacted {Count} sensitive items from input", redactionCount);
            return Task.FromResult(GuardrailResult.Modify(modified));
        }

        return Task.FromResult(GuardrailResult.Allow());
    }

    public Task<GuardrailResult> CheckOutputAsync(
        string output,
        CancellationToken cancellationToken = default)
    {
        var (modified, redactionCount) = RedactSensitiveData(output);
        
        if (redactionCount > 0)
        {
            _logger.LogInformation("Redacted {Count} sensitive items from output", redactionCount);
            return Task.FromResult(GuardrailResult.Modify(modified));
        }

        return Task.FromResult(GuardrailResult.Allow());
    }

    private (string Result, int Count) RedactSensitiveData(string content)
    {
        var result = content;
        var totalCount = 0;

        foreach (var (pattern, replacement) in _redactionPatterns)
        {
            var matches = pattern.Matches(result);
            totalCount += matches.Count;
            result = pattern.Replace(result, replacement);
        }

        return (result, totalCount);
    }

    private List<(Regex, string)> BuildRedactionPatterns()
    {
        var patterns = new List<(Regex, string)>
        {
            // Email addresses
            (new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled),
             "[EMAIL_REDACTED]"),
            
            // Phone numbers
            (new Regex(@"\b(\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled),
             "[PHONE_REDACTED]"),
            
            // SSN
            (new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled),
             "[SSN_REDACTED]"),
            
            // Credit card numbers
            (new Regex(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled),
             "[CC_REDACTED]"),
            
            // API keys and tokens
            (new Regex(@"(?i)(api[_-]?key|token|secret|password)\s*[:=]\s*['\"]?[\w\-\.]+['\"]?", RegexOptions.Compiled),
             "[CREDENTIAL_REDACTED]"),
            
            // IP addresses
            (new Regex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled),
             "[IP_REDACTED]")
        };

        // Add custom patterns from config
        foreach (var pattern in _config.SensitivePatterns)
        {
            try
            {
                patterns.Add((new Regex(pattern, RegexOptions.Compiled), "[REDACTED]"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid sensitive pattern: {Pattern}", pattern);
            }
        }

        return patterns;
    }
}
```

---

## Day 5-7: Safety Pipeline

### 1. Guardrail Pipeline

```csharp
namespace DawningAgents.Core.Safety;

using Microsoft.Extensions.Logging;

/// <summary>
/// Pipeline for executing guardrails
/// </summary>
public class GuardrailPipeline
{
    private readonly List<IGuardrail> _guardrails = [];
    private readonly ILogger<GuardrailPipeline> _logger;

    public GuardrailPipeline(ILogger<GuardrailPipeline> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Add a guardrail to the pipeline
    /// </summary>
    public GuardrailPipeline Add(IGuardrail guardrail)
    {
        _guardrails.Add(guardrail);
        _guardrails.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        return this;
    }

    /// <summary>
    /// Process input through all guardrails
    /// </summary>
    public async Task<PipelineResult> ProcessInputAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return await ProcessAsync(input, true, cancellationToken);
    }

    /// <summary>
    /// Process output through all guardrails
    /// </summary>
    public async Task<PipelineResult> ProcessOutputAsync(
        string output,
        CancellationToken cancellationToken = default)
    {
        return await ProcessAsync(output, false, cancellationToken);
    }

    private async Task<PipelineResult> ProcessAsync(
        string content,
        bool isInput,
        CancellationToken cancellationToken)
    {
        var currentContent = content;
        var results = new List<(string Guardrail, GuardrailResult Result)>();

        foreach (var guardrail in _guardrails)
        {
            try
            {
                var result = isInput
                    ? await guardrail.CheckInputAsync(currentContent, cancellationToken)
                    : await guardrail.CheckOutputAsync(currentContent, cancellationToken);

                results.Add((guardrail.Name, result));

                switch (result.Action)
                {
                    case GuardrailAction.Block:
                        _logger.LogWarning("Content blocked by {Guardrail}: {Reason}",
                            guardrail.Name, result.BlockReason);
                        return new PipelineResult
                        {
                            IsAllowed = false,
                            Content = currentContent,
                            BlockedBy = guardrail.Name,
                            BlockReason = result.BlockReason,
                            GuardrailResults = results
                        };

                    case GuardrailAction.Modify:
                        if (!string.IsNullOrEmpty(result.ModifiedContent))
                        {
                            _logger.LogInformation("Content modified by {Guardrail}", guardrail.Name);
                            currentContent = result.ModifiedContent;
                        }
                        break;

                    case GuardrailAction.Warn:
                        _logger.LogWarning("Warning from {Guardrail}: {Reason}",
                            guardrail.Name, result.BlockReason);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Guardrail {Guardrail} failed", guardrail.Name);
                // Continue with next guardrail
            }
        }

        return new PipelineResult
        {
            IsAllowed = true,
            Content = currentContent,
            GuardrailResults = results
        };
    }
}

/// <summary>
/// Result from guardrail pipeline
/// </summary>
public record PipelineResult
{
    public bool IsAllowed { get; init; }
    public required string Content { get; init; }
    public string? BlockedBy { get; init; }
    public string? BlockReason { get; init; }
    public IReadOnlyList<(string Guardrail, GuardrailResult Result)> GuardrailResults { get; init; } = [];
}
```

### 2. Safe Agent Wrapper

```csharp
namespace DawningAgents.Core.Safety;

using Microsoft.Extensions.Logging;

/// <summary>
/// Wraps an agent with safety guardrails
/// </summary>
public class SafeAgent : IAgent
{
    private readonly IAgent _innerAgent;
    private readonly GuardrailPipeline _pipeline;
    private readonly SafetyConfig _config;
    private readonly ILogger<SafeAgent> _logger;

    public string Name => $"Safe({_innerAgent.Name})";

    public SafeAgent(
        IAgent innerAgent,
        GuardrailPipeline pipeline,
        SafetyConfig config,
        ILogger<SafeAgent> logger)
    {
        _innerAgent = innerAgent;
        _pipeline = pipeline;
        _config = config;
        _logger = logger;
    }

    public async Task<AgentResponse> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Create timeout cancellation
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_config.RequestTimeout);

        try
        {
            // Validate input
            var inputResult = await _pipeline.ProcessInputAsync(context.Input, cts.Token);
            if (!inputResult.IsAllowed)
            {
                return CreateBlockedResponse(inputResult, startTime);
            }

            // Modify context with sanitized input
            var safeContext = context with
            {
                Input = inputResult.Content,
                MaxIterations = Math.Min(context.MaxIterations, _config.MaxIterations)
            };

            // Execute inner agent
            var response = await _innerAgent.ExecuteAsync(safeContext, cts.Token);

            // Validate output
            var outputResult = await _pipeline.ProcessOutputAsync(response.Output, cts.Token);
            if (!outputResult.IsAllowed)
            {
                return CreateBlockedResponse(outputResult, startTime);
            }

            // Return response with sanitized output
            return response with
            {
                Output = outputResult.Content,
                Metadata = new Dictionary<string, object>(response.Metadata)
                {
                    ["safety_checked"] = true,
                    ["guardrails_applied"] = outputResult.GuardrailResults.Count
                }
            };
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Request timed out after {Timeout}", _config.RequestTimeout);
            return new AgentResponse
            {
                Output = "Request timed out. Please try again with a simpler query.",
                IsSuccess = false,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private AgentResponse CreateBlockedResponse(PipelineResult result, DateTime startTime)
    {
        return new AgentResponse
        {
            Output = $"I cannot process this request. Reason: {result.BlockReason}",
            IsSuccess = false,
            Duration = DateTime.UtcNow - startTime,
            Metadata = new Dictionary<string, object>
            {
                ["blocked"] = true,
                ["blocked_by"] = result.BlockedBy ?? "unknown",
                ["block_reason"] = result.BlockReason ?? "unknown"
            }
        };
    }
}
```

### 3. Rate Limiter

```csharp
namespace DawningAgents.Core.Safety;

using System.Collections.Concurrent;

/// <summary>
/// Rate limiting for agent requests
/// </summary>
public class RateLimiter
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private readonly int _tokensPerInterval;
    private readonly TimeSpan _interval;

    public RateLimiter(int tokensPerInterval = 10, TimeSpan? interval = null)
    {
        _tokensPerInterval = tokensPerInterval;
        _interval = interval ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Check if request is allowed
    /// </summary>
    public bool TryAcquire(string clientId, int tokens = 1)
    {
        var bucket = _buckets.GetOrAdd(clientId, _ => new TokenBucket(_tokensPerInterval, _interval));
        return bucket.TryConsume(tokens);
    }

    /// <summary>
    /// Get remaining tokens for client
    /// </summary>
    public int GetRemainingTokens(string clientId)
    {
        if (_buckets.TryGetValue(clientId, out var bucket))
        {
            return bucket.GetAvailableTokens();
        }
        return _tokensPerInterval;
    }

    /// <summary>
    /// Get time until next refill
    /// </summary>
    public TimeSpan GetTimeUntilRefill(string clientId)
    {
        if (_buckets.TryGetValue(clientId, out var bucket))
        {
            return bucket.GetTimeUntilRefill();
        }
        return TimeSpan.Zero;
    }

    private class TokenBucket
    {
        private readonly int _maxTokens;
        private readonly TimeSpan _refillInterval;
        private int _tokens;
        private DateTime _lastRefill;
        private readonly object _lock = new();

        public TokenBucket(int maxTokens, TimeSpan refillInterval)
        {
            _maxTokens = maxTokens;
            _refillInterval = refillInterval;
            _tokens = maxTokens;
            _lastRefill = DateTime.UtcNow;
        }

        public bool TryConsume(int tokens)
        {
            lock (_lock)
            {
                Refill();
                if (_tokens >= tokens)
                {
                    _tokens -= tokens;
                    return true;
                }
                return false;
            }
        }

        public int GetAvailableTokens()
        {
            lock (_lock)
            {
                Refill();
                return _tokens;
            }
        }

        public TimeSpan GetTimeUntilRefill()
        {
            lock (_lock)
            {
                var elapsed = DateTime.UtcNow - _lastRefill;
                if (elapsed >= _refillInterval)
                    return TimeSpan.Zero;
                return _refillInterval - elapsed;
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastRefill;
            if (elapsed >= _refillInterval)
            {
                _tokens = _maxTokens;
                _lastRefill = now;
            }
        }
    }
}
```

### 4. Audit Logger

```csharp
namespace DawningAgents.Core.Safety;

using Microsoft.Extensions.Logging;

/// <summary>
/// Logs all agent interactions for audit
/// </summary>
public class AuditLogger
{
    private readonly ILogger<AuditLogger> _logger;
    private readonly List<IAuditSink> _sinks = [];

    public AuditLogger(ILogger<AuditLogger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Add an audit sink
    /// </summary>
    public void AddSink(IAuditSink sink)
    {
        _sinks.Add(sink);
    }

    /// <summary>
    /// Log an agent interaction
    /// </summary>
    public async Task LogAsync(AuditEntry entry)
    {
        _logger.LogInformation(
            "Audit: {Action} by {Agent} - Success: {Success}",
            entry.Action, entry.AgentName, entry.IsSuccess);

        foreach (var sink in _sinks)
        {
            try
            {
                await sink.WriteAsync(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write to audit sink {Sink}", sink.GetType().Name);
            }
        }
    }
}

/// <summary>
/// Audit entry for logging
/// </summary>
public record AuditEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required string AgentName { get; init; }
    public required string Action { get; init; }
    public required string Input { get; init; }
    public string? Output { get; init; }
    public bool IsSuccess { get; init; }
    public TimeSpan Duration { get; init; }
    public string? UserId { get; init; }
    public string? SessionId { get; init; }
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Sink for audit entries
/// </summary>
public interface IAuditSink
{
    Task WriteAsync(AuditEntry entry);
}

/// <summary>
/// File-based audit sink
/// </summary>
public class FileAuditSink : IAuditSink
{
    private readonly string _path;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public FileAuditSink(string path)
    {
        _path = path;
    }

    public async Task WriteAsync(AuditEntry entry)
    {
        await _semaphore.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(entry);
            await File.AppendAllTextAsync(_path, json + Environment.NewLine);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

---

## Complete Example

```csharp
// Create safety configuration
var safetyConfig = new SafetyConfig
{
    MaxIterations = 10,
    RequestTimeout = TimeSpan.FromMinutes(2),
    EnableInputValidation = true,
    EnableOutputFiltering = true,
    BlockedTopics = ["illegal activities", "harmful content"]
};

// Build guardrail pipeline
var pipeline = new GuardrailPipeline(logger)
    .Add(new InputValidator(safetyConfig, loggerFactory.CreateLogger<InputValidator>()))
    .Add(new SensitiveDataFilter(safetyConfig, loggerFactory.CreateLogger<SensitiveDataFilter>()))
    .Add(new ContentModerator(llm, loggerFactory.CreateLogger<ContentModerator>()));

// Create safe agent
var innerAgent = new ReActAgent(llm, loggerFactory.CreateLogger<ReActAgent>());
var safeAgent = new SafeAgent(
    innerAgent,
    pipeline,
    safetyConfig,
    loggerFactory.CreateLogger<SafeAgent>());

// Setup rate limiting
var rateLimiter = new RateLimiter(tokensPerInterval: 10, interval: TimeSpan.FromMinutes(1));

// Setup audit logging
var auditLogger = new AuditLogger(loggerFactory.CreateLogger<AuditLogger>());
auditLogger.AddSink(new FileAuditSink("audit.log"));

// Execute with safety
var clientId = "user-123";
if (rateLimiter.TryAcquire(clientId))
{
    var response = await safeAgent.ExecuteAsync(new AgentContext
    {
        Input = "Help me with my task"
    });

    await auditLogger.LogAsync(new AuditEntry
    {
        AgentName = safeAgent.Name,
        Action = "execute",
        Input = "Help me with my task",
        Output = response.Output,
        IsSuccess = response.IsSuccess,
        Duration = response.Duration,
        UserId = clientId
    });
}
else
{
    Console.WriteLine($"Rate limited. Try again in {rateLimiter.GetTimeUntilRefill(clientId)}");
}
```

---

## Summary

### Week 9 Deliverables

```
src/DawningAgents.Core/
└── Safety/
    ├── SafetyConfig.cs              # Configuration
    ├── IGuardrail.cs                # Guardrail interface
    ├── GuardrailResult.cs           # Result model
    ├── GuardrailPipeline.cs         # Pipeline execution
    ├── SafeAgent.cs                 # Safe wrapper
    ├── RateLimiter.cs               # Rate limiting
    ├── AuditLogger.cs               # Audit logging
    └── Guardrails/
        ├── InputValidator.cs        # Input validation
        ├── ContentModerator.cs      # LLM moderation
        └── SensitiveDataFilter.cs   # Data redaction
```

### Safety Layers

| Layer | Purpose |
|-------|---------|
| **Input Validation** | Block malicious input |
| **Content Moderation** | Filter harmful content |
| **Data Filtering** | Redact sensitive data |
| **Rate Limiting** | Prevent abuse |
| **Audit Logging** | Track all interactions |

### Next: Week 10

Week 10 will cover Human-in-the-Loop:
- Confirmation patterns
- Approval workflows
- Escalation handling
