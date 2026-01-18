# 第9周：安全与护栏

> 第五阶段：高级主题
> 第9周学习材料：输入/输出验证、内容过滤与安全模式

---

## 第1-2天：安全基础

### 1. 为什么安全很重要

```text
┌─────────────────────────────────────────────────────────────────┐
│                      Agent安全问题                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │   提示词       │    │    有害        │    │    资源        │ │
│  │   注入攻击     │    │    内容        │    │    滥用        │ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │    数据        │    │    意外        │    │    无限        │ │
│  │    泄露        │    │    操作        │    │    循环        │ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. 安全配置

```csharp
namespace Dawning.Agents.Core.Safety;

/// <summary>
/// Agent的安全配置
/// </summary>
public record SafetyConfig
{
    /// <summary>
    /// 每个请求的最大迭代次数
    /// </summary>
    public int MaxIterations { get; init; } = 10;
    
    /// <summary>
    /// 每个响应的最大token数
    /// </summary>
    public int MaxTokensPerResponse { get; init; } = 4000;
    
    /// <summary>
    /// 每个会话的最大总token数
    /// </summary>
    public int MaxTokensPerSession { get; init; } = 100000;
    
    /// <summary>
    /// 请求超时时间
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// 启用输入验证
    /// </summary>
    public bool EnableInputValidation { get; init; } = true;
    
    /// <summary>
    /// 启用输出过滤
    /// </summary>
    public bool EnableOutputFiltering { get; init; } = true;
    
    /// <summary>
    /// 启用内容审核
    /// </summary>
    public bool EnableContentModeration { get; init; } = true;
    
    /// <summary>
    /// 被阻止的话题
    /// </summary>
    public IReadOnlyList<string> BlockedTopics { get; init; } = [];
    
    /// <summary>
    /// Web请求允许的域名
    /// </summary>
    public IReadOnlyList<string> AllowedDomains { get; init; } = [];
    
    /// <summary>
    /// 需要脱敏的敏感数据模式
    /// </summary>
    public IReadOnlyList<string> SensitivePatterns { get; init; } = [
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",  // 邮箱
        @"\b\d{3}-\d{2}-\d{4}\b",  // SSN
        @"\b\d{16}\b",  // 信用卡
        @"(?i)(password|secret|api[_-]?key|token)\s*[:=]\s*\S+"  // 凭证
    ];
}
```

### 3. 护栏接口

```csharp
namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 安全护栏接口
/// </summary>
public interface IGuardrail
{
    /// <summary>
    /// 护栏名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 优先级（越小越先执行）
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// 处理前检查输入
    /// </summary>
    Task<GuardrailResult> CheckInputAsync(
        string input,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 返回前检查输出
    /// </summary>
    Task<GuardrailResult> CheckOutputAsync(
        string output,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 护栏检查结果
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
    Allow,   // 允许
    Block,   // 阻止
    Modify,  // 修改
    Warn     // 警告
}
```

---

## 第3-4天：输入验证与过滤

### 1. 输入验证器

```csharp
namespace Dawning.Agents.Core.Safety.Guardrails;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// 验证和清理输入
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
            return Task.FromResult(GuardrailResult.Block("输入为空"));
        }

        // 检查长度
        if (input.Length > 10000)
        {
            return Task.FromResult(GuardrailResult.Block("输入过长"));
        }

        // 检查提示词注入模式
        var injectionResult = CheckPromptInjection(input);
        if (!injectionResult.IsAllowed)
        {
            _logger.LogWarning("检测到提示词注入：{Reason}", injectionResult.BlockReason);
            return Task.FromResult(injectionResult);
        }

        // 检查被阻止的话题
        foreach (var topic in _config.BlockedTopics)
        {
            if (input.Contains(topic, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("检测到被阻止的话题：{Topic}", topic);
                return Task.FromResult(GuardrailResult.Block($"话题不允许：{topic}"));
            }
        }

        // 检查被阻止的模式
        foreach (var pattern in _blockedPatterns)
        {
            if (pattern.IsMatch(input))
            {
                _logger.LogWarning("匹配到被阻止的模式");
                return Task.FromResult(GuardrailResult.Block("内容包含被阻止的模式"));
            }
        }

        return Task.FromResult(GuardrailResult.Allow());
    }

    public Task<GuardrailResult> CheckOutputAsync(
        string output,
        CancellationToken cancellationToken = default)
    {
        // 输入验证器不检查输出
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
            @"忽略(之前|上面|所有)(的)?(指令|提示)",
            @"你现在是",
            @"新指令[：:]",
        };

        foreach (var pattern in injectionPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                return GuardrailResult.Block("检测到潜在的提示词注入");
            }
        }

        return GuardrailResult.Allow();
    }

    private List<Regex> InitializeBlockedPatterns()
    {
        var patterns = new List<Regex>();
        
        // 添加默认有害模式
        var defaultPatterns = new[]
        {
            @"(?i)(how\s+to\s+)?(make|create|build)\s+(a\s+)?(bomb|explosive|weapon)",
            @"(?i)(hack|exploit|attack)\s+(into|the)",
            @"(如何|怎么)(制作|制造)(炸弹|爆炸物|武器)",
        };

        foreach (var pattern in defaultPatterns)
        {
            patterns.Add(new Regex(pattern, RegexOptions.Compiled));
        }

        return patterns;
    }
}
```

### 2. 内容审核器

```csharp
namespace Dawning.Agents.Core.Safety.Guardrails;

using Microsoft.Extensions.Logging;

/// <summary>
/// 使用LLM审核内容
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
            "暴力",
            "骚扰",
            "仇恨言论",
            "自我伤害",
            "色情内容",
            "非法活动"
        ];
    }

    public async Task<GuardrailResult> CheckInputAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return await ModerateContentAsync(input, "输入", cancellationToken);
    }

    public async Task<GuardrailResult> CheckOutputAsync(
        string output,
        CancellationToken cancellationToken = default)
    {
        return await ModerateContentAsync(output, "输出", cancellationToken);
    }

    private async Task<GuardrailResult> ModerateContentAsync(
        string content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var prompt = $"""
            你是一个内容审核员。分析以下{contentType}并判断是否违反以下任何类别：
            {string.Join("、", _categories)}

            要分析的内容：
            {content}

            以JSON对象响应：
            {{
                "is_safe": true/false,
                "violated_categories": ["类别1", "类别2"],
                "confidence": 0.0-1.0,
                "reason": "如果不安全的解释"
            }}
            """;

        try
        {
            var response = await _llm.GenerateAsync(prompt, cancellationToken: cancellationToken);
            var result = ParseModerationResult(response.Content);

            if (!result.IsSafe)
            {
                _logger.LogWarning("内容审核失败：{Reason}", result.Reason);
                return GuardrailResult.Block(result.Reason ?? "内容违反安全政策");
            }

            return GuardrailResult.Allow();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "内容审核错误");
            // 根据配置决定是放行还是阻止
            return GuardrailResult.Warn("审核检查失败");
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
            // 解析失败
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

### 3. 敏感数据过滤器

```csharp
namespace Dawning.Agents.Core.Safety.Guardrails;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// 过滤和脱敏敏感数据
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
            _logger.LogInformation("从输入中脱敏了 {Count} 个敏感项", redactionCount);
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
            _logger.LogInformation("从输出中脱敏了 {Count} 个敏感项", redactionCount);
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
            // 邮箱地址
            (new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled),
             "[邮箱已脱敏]"),
            
            // 电话号码
            (new Regex(@"\b(\+?86[-.\s]?)?1[3-9]\d{9}\b", RegexOptions.Compiled),
             "[电话已脱敏]"),
            
            // 身份证号
            (new Regex(@"\b\d{17}[\dXx]\b", RegexOptions.Compiled),
             "[身份证已脱敏]"),
            
            // 银行卡号
            (new Regex(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled),
             "[银行卡已脱敏]"),
            
            // API密钥和令牌
            (new Regex(@"(?i)(api[_-]?key|token|secret|password|密码|密钥)\s*[:=：]\s*['\"]?[\w\-\.]+['\"]?", RegexOptions.Compiled),
             "[凭证已脱敏]"),
            
            // IP地址
            (new Regex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled),
             "[IP已脱敏]")
        };

        // 从配置添加自定义模式
        foreach (var pattern in _config.SensitivePatterns)
        {
            try
            {
                patterns.Add((new Regex(pattern, RegexOptions.Compiled), "[已脱敏]"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无效的敏感模式：{Pattern}", pattern);
            }
        }

        return patterns;
    }
}
```

---

## 第5-7天：安全流水线

### 1. 护栏流水线

```csharp
namespace Dawning.Agents.Core.Safety;

using Microsoft.Extensions.Logging;

/// <summary>
/// 执行护栏的流水线
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
    /// 向流水线添加护栏
    /// </summary>
    public GuardrailPipeline Add(IGuardrail guardrail)
    {
        _guardrails.Add(guardrail);
        _guardrails.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        return this;
    }

    /// <summary>
    /// 通过所有护栏处理输入
    /// </summary>
    public async Task<PipelineResult> ProcessInputAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return await ProcessAsync(input, true, cancellationToken);
    }

    /// <summary>
    /// 通过所有护栏处理输出
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
                        _logger.LogWarning("内容被 {Guardrail} 阻止：{Reason}",
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
                            _logger.LogInformation("内容被 {Guardrail} 修改", guardrail.Name);
                            currentContent = result.ModifiedContent;
                        }
                        break;

                    case GuardrailAction.Warn:
                        _logger.LogWarning("来自 {Guardrail} 的警告：{Reason}",
                            guardrail.Name, result.BlockReason);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "护栏 {Guardrail} 失败", guardrail.Name);
                // 继续下一个护栏
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
/// 护栏流水线的结果
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

### 2. 安全Agent包装器

```csharp
namespace Dawning.Agents.Core.Safety;

using Microsoft.Extensions.Logging;

/// <summary>
/// 用安全护栏包装Agent
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

        // 创建超时取消
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_config.RequestTimeout);

        try
        {
            // 验证输入
            var inputResult = await _pipeline.ProcessInputAsync(context.Input, cts.Token);
            if (!inputResult.IsAllowed)
            {
                return CreateBlockedResponse(inputResult, startTime);
            }

            // 用清理后的输入修改上下文
            var safeContext = context with
            {
                Input = inputResult.Content,
                MaxIterations = Math.Min(context.MaxIterations, _config.MaxIterations)
            };

            // 执行内部Agent
            var response = await _innerAgent.ExecuteAsync(safeContext, cts.Token);

            // 验证输出
            var outputResult = await _pipeline.ProcessOutputAsync(response.Output, cts.Token);
            if (!outputResult.IsAllowed)
            {
                return CreateBlockedResponse(outputResult, startTime);
            }

            // 返回清理后输出的响应
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
            _logger.LogWarning("请求在 {Timeout} 后超时", _config.RequestTimeout);
            return new AgentResponse
            {
                Output = "请求超时。请尝试使用更简单的查询。",
                IsSuccess = false,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private AgentResponse CreateBlockedResponse(PipelineResult result, DateTime startTime)
    {
        return new AgentResponse
        {
            Output = $"我无法处理这个请求。原因：{result.BlockReason}",
            IsSuccess = false,
            Duration = DateTime.UtcNow - startTime,
            Metadata = new Dictionary<string, object>
            {
                ["blocked"] = true,
                ["blocked_by"] = result.BlockedBy ?? "未知",
                ["block_reason"] = result.BlockReason ?? "未知"
            }
        };
    }
}
```

### 3. 速率限制器

```csharp
namespace Dawning.Agents.Core.Safety;

using System.Collections.Concurrent;

/// <summary>
/// Agent请求的速率限制
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
    /// 检查请求是否被允许
    /// </summary>
    public bool TryAcquire(string clientId, int tokens = 1)
    {
        var bucket = _buckets.GetOrAdd(clientId, _ => new TokenBucket(_tokensPerInterval, _interval));
        return bucket.TryConsume(tokens);
    }

    /// <summary>
    /// 获取客户端剩余的令牌数
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
    /// 获取下次刷新的时间
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

### 4. 审计日志器

```csharp
namespace Dawning.Agents.Core.Safety;

using Microsoft.Extensions.Logging;

/// <summary>
/// 记录所有Agent交互以供审计
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
    /// 添加审计接收器
    /// </summary>
    public void AddSink(IAuditSink sink)
    {
        _sinks.Add(sink);
    }

    /// <summary>
    /// 记录Agent交互
    /// </summary>
    public async Task LogAsync(AuditEntry entry)
    {
        _logger.LogInformation(
            "审计：{Action} 由 {Agent} - 成功：{Success}",
            entry.Action, entry.AgentName, entry.IsSuccess);

        foreach (var sink in _sinks)
        {
            try
            {
                await sink.WriteAsync(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入审计接收器 {Sink} 失败", sink.GetType().Name);
            }
        }
    }
}

/// <summary>
/// 审计条目
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
/// 审计条目接收器
/// </summary>
public interface IAuditSink
{
    Task WriteAsync(AuditEntry entry);
}

/// <summary>
/// 基于文件的审计接收器
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

## 完整示例

```csharp
// 创建安全配置
var safetyConfig = new SafetyConfig
{
    MaxIterations = 10,
    RequestTimeout = TimeSpan.FromMinutes(2),
    EnableInputValidation = true,
    EnableOutputFiltering = true,
    BlockedTopics = ["非法活动", "有害内容"]
};

// 构建护栏流水线
var pipeline = new GuardrailPipeline(logger)
    .Add(new InputValidator(safetyConfig, loggerFactory.CreateLogger<InputValidator>()))
    .Add(new SensitiveDataFilter(safetyConfig, loggerFactory.CreateLogger<SensitiveDataFilter>()))
    .Add(new ContentModerator(llm, loggerFactory.CreateLogger<ContentModerator>()));

// 创建安全Agent
var innerAgent = new ReActAgent(llm, loggerFactory.CreateLogger<ReActAgent>());
var safeAgent = new SafeAgent(
    innerAgent,
    pipeline,
    safetyConfig,
    loggerFactory.CreateLogger<SafeAgent>());

// 设置速率限制
var rateLimiter = new RateLimiter(tokensPerInterval: 10, interval: TimeSpan.FromMinutes(1));

// 设置审计日志
var auditLogger = new AuditLogger(loggerFactory.CreateLogger<AuditLogger>());
auditLogger.AddSink(new FileAuditSink("audit.log"));

// 安全执行
var clientId = "user-123";
if (rateLimiter.TryAcquire(clientId))
{
    var response = await safeAgent.ExecuteAsync(new AgentContext
    {
        Input = "帮我完成我的任务"
    });

    await auditLogger.LogAsync(new AuditEntry
    {
        AgentName = safeAgent.Name,
        Action = "execute",
        Input = "帮我完成我的任务",
        Output = response.Output,
        IsSuccess = response.IsSuccess,
        Duration = response.Duration,
        UserId = clientId
    });
}
else
{
    Console.WriteLine($"请求受限。请在 {rateLimiter.GetTimeUntilRefill(clientId)} 后重试");
}
```

---

## 总结

### 第9周交付物

```
src/Dawning.Agents.Core/
└── Safety/
    ├── SafetyConfig.cs              # 配置
    ├── IGuardrail.cs                # 护栏接口
    ├── GuardrailResult.cs           # 结果模型
    ├── GuardrailPipeline.cs         # 流水线执行
    ├── SafeAgent.cs                 # 安全包装器
    ├── RateLimiter.cs               # 速率限制
    ├── AuditLogger.cs               # 审计日志
    └── Guardrails/
        ├── InputValidator.cs        # 输入验证
        ├── ContentModerator.cs      # LLM审核
        └── SensitiveDataFilter.cs   # 数据脱敏
```

### 安全层次

| 层次 | 目的 |
|------|------|
| **输入验证** | 阻止恶意输入 |
| **内容审核** | 过滤有害内容 |
| **数据过滤** | 脱敏敏感数据 |
| **速率限制** | 防止滥用 |
| **审计日志** | 追踪所有交互 |

### 下一步：第10周

第10周将涵盖人机协作：
- 确认模式
- 审批工作流
- 升级处理
