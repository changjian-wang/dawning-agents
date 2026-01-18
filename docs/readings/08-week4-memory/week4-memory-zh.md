# Week 4: 对话管理与记忆

> Phase 2: 单 Agent 开发核心技能
> Week 4 学习资料：管理对话、记忆和 Agent 状态

---

## Day 1-2: 对话历史管理

### 1. 为什么记忆很重要

Agent 需要记忆来：
- **维护上下文** 跨多轮对话
- **记住用户偏好** 和先前的决定
- **跟踪任务进度** 随时间推移
- **避免重复** 和不一致

```text
┌─────────────────────────────────────────────────────────────────┐
│                    Agent 中的记忆类型                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────┐  ┌──────────────────┐  ┌────────────────┐ │
│  │  短期记忆        │  │  长期记忆        │  │  工作记忆      │ │
│  │  Short-term     │  │  Long-term       │  │  Working       │ │
│  ├──────────────────┤  ├──────────────────┤  ├────────────────┤ │
│  │ • 当前对话       │  │ • 用户档案       │  │ • 当前任务     │ │
│  │ • 最近上下文     │  │ • 历史会话       │  │ • 活跃目标     │ │
│  │ • 基于缓冲区     │  │ • 向量数据库     │  │ • 草稿本       │ │
│  └──────────────────┘  └──────────────────┘  └────────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. 记忆接口设计

```csharp
namespace Dawning.Agents.Core.Memory;

/// <summary>
/// 表示对话历史中的消息
/// </summary>
public record ConversationMessage
{
    /// <summary>
    /// 消息的唯一标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 角色："user"、"assistant" 或 "system"
    /// </summary>
    public required string Role { get; init; }
    
    /// <summary>
    /// 消息内容
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// 消息创建时间
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// 可选的元数据（例如工具调用、token 数量）
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }
    
    /// <summary>
    /// 此消息的估计 token 数量
    /// </summary>
    public int? TokenCount { get; init; }
}

/// <summary>
/// 对话记忆管理接口
/// </summary>
public interface IConversationMemory
{
    /// <summary>
    /// 向记忆添加消息
    /// </summary>
    Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取记忆中的所有消息
    /// </summary>
    Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取格式化为 LLM 上下文的消息
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 清除记忆中的所有消息
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取当前 token 数量
    /// </summary>
    Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default);
}
```

### 3. 缓冲记忆实现

最简单的记忆类型 - 将所有消息存储在列表中：

```csharp
namespace Dawning.Agents.Core.Memory;

using Dawning.Agents.Core.LLM;

/// <summary>
/// 存储所有消息的简单缓冲记忆
/// </summary>
public class BufferMemory : IConversationMemory
{
    private readonly List<ConversationMessage> _messages = [];
    private readonly ITokenCounter _tokenCounter;
    private readonly object _lock = new();

    public BufferMemory(ITokenCounter tokenCounter)
    {
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
    }

    public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // 如果未提供则计算 token 数量
            if (message.TokenCount == null)
            {
                var tokenCount = _tokenCounter.CountTokens(message.Content);
                message = message with { TokenCount = tokenCount };
            }
            
            _messages.Add(message);
        }
        
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ConversationMessage>>(_messages.ToList());
        }
    }

    public Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var messages = _messages.AsEnumerable();
            
            if (maxTokens.HasValue)
            {
                // 取适合 token 限制的最近消息
                messages = TrimToTokenLimit(_messages, maxTokens.Value);
            }
            
            var result = messages
                .Select(m => new ChatMessage(m.Role, m.Content))
                .ToList();
                
            return Task.FromResult<IReadOnlyList<ChatMessage>>(result);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _messages.Clear();
        }
        
        return Task.CompletedTask;
    }

    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var total = _messages.Sum(m => m.TokenCount ?? 0);
            return Task.FromResult(total);
        }
    }

    private IEnumerable<ConversationMessage> TrimToTokenLimit(
        List<ConversationMessage> messages,
        int maxTokens)
    {
        var result = new List<ConversationMessage>();
        var tokenCount = 0;
        
        // 从最近的消息开始
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var msgTokens = messages[i].TokenCount ?? 0;
            if (tokenCount + msgTokens > maxTokens)
                break;
                
            result.Insert(0, messages[i]);
            tokenCount += msgTokens;
        }
        
        return result;
    }
}
```

### 4. 窗口记忆实现

只保留最后 N 条消息：

```csharp
namespace Dawning.Agents.Core.Memory;

/// <summary>
/// 只保留最后 N 条消息的记忆
/// </summary>
public class WindowMemory : IConversationMemory
{
    private readonly List<ConversationMessage> _messages = [];
    private readonly ITokenCounter _tokenCounter;
    private readonly int _windowSize;
    private readonly object _lock = new();

    public WindowMemory(ITokenCounter tokenCounter, int windowSize = 10)
    {
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _windowSize = windowSize > 0 ? windowSize : throw new ArgumentException("窗口大小必须为正数");
    }

    public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (message.TokenCount == null)
            {
                var tokenCount = _tokenCounter.CountTokens(message.Content);
                message = message with { TokenCount = tokenCount };
            }
            
            _messages.Add(message);
            
            // 裁剪到窗口大小
            while (_messages.Count > _windowSize)
            {
                _messages.RemoveAt(0);
            }
        }
        
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ConversationMessage>>(_messages.ToList());
        }
    }

    public Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var result = _messages
                .Select(m => new ChatMessage(m.Role, m.Content))
                .ToList();
                
            return Task.FromResult<IReadOnlyList<ChatMessage>>(result);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _messages.Clear();
        }
        
        return Task.CompletedTask;
    }

    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_messages.Sum(m => m.TokenCount ?? 0));
        }
    }
}
```

---

## Day 3-4: Token 管理

### 1. 理解 Token 计数

Token 是 LLM 处理的基本单位。管理 token 至关重要，因为：
- LLM 有**上下文窗口限制**（4K、8K、128K 等）
- **成本**按 token 计算
- 过多的上下文会导致**性能**下降

#### Token 估算规则

| 内容类型 | 大致比例 |
|---------|---------|
| 英文文本 | ~4 个字符/token |
| 中文文本 | ~1-2 个字符/token |
| 代码 | ~3-4 个字符/token |
| 空白字符 | 变化不定 |

### 2. Token 计数器接口

```csharp
namespace Dawning.Agents.Core.Tokens;

/// <summary>
/// 计算文本中 token 数量的接口
/// </summary>
public interface ITokenCounter
{
    /// <summary>
    /// 计算给定文本中的 token 数量
    /// </summary>
    int CountTokens(string text);
    
    /// <summary>
    /// 计算消息列表的 token 数量（包括角色开销）
    /// </summary>
    int CountTokens(IEnumerable<ChatMessage> messages);
    
    /// <summary>
    /// 获取此计数器对应的模型名称
    /// </summary>
    string ModelName { get; }
    
    /// <summary>
    /// 获取最大上下文窗口大小
    /// </summary>
    int MaxContextTokens { get; }
}
```

### 3. 基于 Tiktoken 的实现

使用 SharpToken 库（tiktoken 的 C# 移植版）：

```csharp
namespace Dawning.Agents.Core.Tokens;

using SharpToken;

/// <summary>
/// 使用 tiktoken 编码的 token 计数器
/// </summary>
public class TiktokenCounter : ITokenCounter
{
    private readonly GptEncoding _encoding;
    private readonly int _tokensPerMessage;
    private readonly int _tokensPerName;

    public string ModelName { get; }
    public int MaxContextTokens { get; }

    // 模型配置
    private static readonly Dictionary<string, (int maxTokens, int perMessage, int perName)> ModelConfigs = new()
    {
        ["gpt-4"] = (8192, 3, 1),
        ["gpt-4-32k"] = (32768, 3, 1),
        ["gpt-4-turbo"] = (128000, 3, 1),
        ["gpt-4o"] = (128000, 3, 1),
        ["gpt-3.5-turbo"] = (16385, 3, 1),
        ["gpt-3.5-turbo-16k"] = (16385, 3, 1),
    };

    public TiktokenCounter(string modelName = "gpt-4")
    {
        ModelName = modelName;
        
        // 获取模型的编码
        _encoding = GptEncoding.GetEncodingForModel(modelName);
        
        // 获取模型配置
        if (ModelConfigs.TryGetValue(modelName, out var config))
        {
            MaxContextTokens = config.maxTokens;
            _tokensPerMessage = config.perMessage;
            _tokensPerName = config.perName;
        }
        else
        {
            // 默认使用 GPT-4 设置
            MaxContextTokens = 8192;
            _tokensPerMessage = 3;
            _tokensPerName = 1;
        }
    }

    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
            
        return _encoding.Encode(text).Count;
    }

    public int CountTokens(IEnumerable<ChatMessage> messages)
    {
        var totalTokens = 0;
        
        foreach (var message in messages)
        {
            totalTokens += _tokensPerMessage;
            totalTokens += CountTokens(message.Role);
            totalTokens += CountTokens(message.Content);
        }
        
        totalTokens += 3; // 每个回复都以 <|start|>assistant<|message|> 开头
        
        return totalTokens;
    }
}
```

### 4. 简单估算计数器

用于 tiktoken 不可用的场景：

```csharp
namespace Dawning.Agents.Core.Tokens;

/// <summary>
/// 使用基于字符估算的简单 token 计数器
/// </summary>
public class SimpleTokenCounter : ITokenCounter
{
    private const double EnglishCharsPerToken = 4.0;
    private const double ChineseCharsPerToken = 1.5;
    private const int TokensPerMessage = 4;

    public string ModelName { get; }
    public int MaxContextTokens { get; }

    public SimpleTokenCounter(string modelName = "gpt-4", int maxContextTokens = 8192)
    {
        ModelName = modelName;
        MaxContextTokens = maxContextTokens;
    }

    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var englishChars = 0;
        var chineseChars = 0;

        foreach (var c in text)
        {
            if (IsChinese(c))
                chineseChars++;
            else
                englishChars++;
        }

        var tokens = (int)Math.Ceiling(
            englishChars / EnglishCharsPerToken + 
            chineseChars / ChineseCharsPerToken);

        return Math.Max(1, tokens);
    }

    public int CountTokens(IEnumerable<ChatMessage> messages)
    {
        var total = 0;
        
        foreach (var message in messages)
        {
            total += TokensPerMessage;
            total += CountTokens(message.Content);
        }
        
        return total + 3; // 回复预备
    }

    private static bool IsChinese(char c)
    {
        // CJK 统一汉字范围
        return c >= 0x4E00 && c <= 0x9FFF;
    }
}
```

### 5. 带压缩的摘要记忆

```csharp
namespace Dawning.Agents.Core.Memory;

using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Tokens;

/// <summary>
/// 通过摘要旧消息来节省 token 的记忆
/// </summary>
public class SummaryMemory : IConversationMemory
{
    private readonly List<ConversationMessage> _recentMessages = [];
    private string _summary = "";
    private readonly ILLMProvider _llm;
    private readonly ITokenCounter _tokenCounter;
    private readonly int _maxRecentMessages;
    private readonly int _maxTokens;
    private readonly object _lock = new();

    public SummaryMemory(
        ILLMProvider llm,
        ITokenCounter tokenCounter,
        int maxRecentMessages = 6,
        int maxTokens = 2000)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _maxRecentMessages = maxRecentMessages;
        _maxTokens = maxTokens;
    }

    public async Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default)
    {
        ConversationMessage messageWithTokens;
        List<ConversationMessage> messagesToSummarize;

        lock (_lock)
        {
            if (message.TokenCount == null)
            {
                var tokenCount = _tokenCounter.CountTokens(message.Content);
                messageWithTokens = message with { TokenCount = tokenCount };
            }
            else
            {
                messageWithTokens = message;
            }
            
            _recentMessages.Add(messageWithTokens);

            // 检查是否需要摘要
            if (_recentMessages.Count <= _maxRecentMessages)
            {
                return;
            }

            // 取最旧的消息进行摘要（保留最近的）
            var toSummarize = _recentMessages.Count - _maxRecentMessages / 2;
            messagesToSummarize = _recentMessages.Take(toSummarize).ToList();
            _recentMessages.RemoveRange(0, toSummarize);
        }

        // 在锁外进行摘要
        await SummarizeMessagesAsync(messagesToSummarize, cancellationToken);
    }

    private async Task SummarizeMessagesAsync(
        List<ConversationMessage> messages,
        CancellationToken cancellationToken)
    {
        var conversationText = string.Join("\n", messages.Select(m => $"{m.Role}: {m.Content}"));
        
        var prompt = $"""
            总结以下对话，保留关键信息和上下文：
            
            之前的摘要：{(string.IsNullOrEmpty(_summary) ? "无" : _summary)}
            
            新消息：
            {conversationText}
            
            提供一个简洁的摘要，包含：
            1. 讨论的主要话题
            2. 关键决定或结论
            3. 未来对话的重要上下文
            
            摘要：
            """;

        var response = await _llm.ChatAsync(
            [new ChatMessage("user", prompt)],
            new ChatCompletionOptions
            {
                Temperature = 0.3f,
                MaxTokens = 500
            },
            cancellationToken);

        lock (_lock)
        {
            _summary = response.Content;
        }
    }

    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var result = new List<ConversationMessage>();
            
            // 如果存在摘要，作为系统消息添加
            if (!string.IsNullOrEmpty(_summary))
            {
                result.Add(new ConversationMessage
                {
                    Role = "system",
                    Content = $"之前对话的摘要：{_summary}"
                });
            }
            
            result.AddRange(_recentMessages);
            return Task.FromResult<IReadOnlyList<ConversationMessage>>(result);
        }
    }

    public async Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default)
    {
        var messages = await GetMessagesAsync(cancellationToken);
        return messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _recentMessages.Clear();
            _summary = "";
        }
        
        return Task.CompletedTask;
    }

    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var summaryTokens = _tokenCounter.CountTokens(_summary);
            var messageTokens = _recentMessages.Sum(m => m.TokenCount ?? 0);
            return Task.FromResult(summaryTokens + messageTokens);
        }
    }
}
```

---

## Day 5-7: Agent 状态机

### 1. 理解 Agent 状态

Agent 在执行过程中经历各种状态：

```text
┌─────────────────────────────────────────────────────────────────┐
│                    Agent 状态机                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│    ┌────────┐                                                    │
│    │  空闲  │◄───────────────────────────────────┐               │
│    └───┬────┘                                    │               │
│        │ 开始                                    │ 完成/         │
│        ▼                                         │ 错误          │
│    ┌────────────┐                                │               │
│    │   思考     │◄──────────┐                    │               │
│    └───┬────────┘           │                    │               │
│        │ 决定               │                    │               │
│        ▼                    │                    │               │
│    ┌────────────┐     ┌─────┴──────┐             │               │
│    │   行动     │────►│   观察     │             │               │
│    └───┬────────┘     └────────────┘             │               │
│        │                                         │               │
│        │ 结束                                    │               │
│        ▼                                         │               │
│    ┌────────────┐                                │               │
│    │   完成中   │────────────────────────────────┘               │
│    └────────────┘                                                │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. 状态定义

```csharp
namespace Dawning.Agents.Core.Agents;

/// <summary>
/// 表示 Agent 的当前状态
/// </summary>
public enum AgentState
{
    /// <summary>
    /// Agent 空闲，等待输入
    /// </summary>
    Idle,
    
    /// <summary>
    /// Agent 正在处理输入并决定做什么
    /// </summary>
    Thinking,
    
    /// <summary>
    /// Agent 正在执行操作（工具调用）
    /// </summary>
    Acting,
    
    /// <summary>
    /// Agent 正在处理操作的结果
    /// </summary>
    Observing,
    
    /// <summary>
    /// Agent 正在生成最终响应
    /// </summary>
    Completing,
    
    /// <summary>
    /// Agent 已完成执行
    /// </summary>
    Completed,
    
    /// <summary>
    /// Agent 遇到错误
    /// </summary>
    Error,
    
    /// <summary>
    /// Agent 正在等待外部输入
    /// </summary>
    WaitingForInput,
    
    /// <summary>
    /// Agent 执行被取消
    /// </summary>
    Cancelled
}

/// <summary>
/// Agent 状态变化时触发的事件
/// </summary>
public record AgentStateChangedEvent
{
    public required AgentState PreviousState { get; init; }
    public required AgentState CurrentState { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? Message { get; init; }
}
```

### 3. 有状态 Agent 实现

```csharp
namespace Dawning.Agents.Core.Agents;

using Dawning.Agents.Core.LLM;
using Microsoft.Extensions.Logging;

/// <summary>
/// 具有显式状态管理的 Agent
/// </summary>
public class StatefulAgent : AgentBase
{
    private AgentState _state = AgentState.Idle;
    private readonly object _stateLock = new();

    public override string Name { get; }
    public override string Description { get; }

    /// <summary>
    /// 当前 Agent 状态
    /// </summary>
    public AgentState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// 状态变化时触发的事件
    /// </summary>
    public event EventHandler<AgentStateChangedEvent>? StateChanged;

    public StatefulAgent(
        ILLMProvider llm,
        ILogger<StatefulAgent> logger,
        string? name = null,
        string? description = null) : base(llm, logger)
    {
        Name = name ?? "StatefulAgent";
        Description = description ?? "具有显式状态管理的 Agent";
    }

    protected override string GetDefaultSystemPrompt()
    {
        return """
            你是一个有帮助的 AI 助手，可以一步一步解决问题。
            在行动之前仔细思考，并解释你的推理。
            """;
    }

    public override async Task<AgentResponse> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var steps = new List<AgentStep>();
        var scratchpad = new StringBuilder();
        var totalTokens = 0;

        try
        {
            TransitionTo(AgentState.Thinking, "处理输入");

            for (int iteration = 0; iteration < context.MaxIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 思考：决定做什么
                TransitionTo(AgentState.Thinking, $"第 {iteration + 1} 次迭代");

                var prompt = BuildPrompt(context, scratchpad.ToString());
                var response = await LLM.ChatAsync(
                    [new ChatMessage("user", prompt)],
                    new ChatCompletionOptions
                    {
                        SystemPrompt = BuildSystemPrompt(context),
                        Temperature = 0.0f,
                        MaxTokens = 1000
                    },
                    cancellationToken);

                totalTokens += response.TotalTokens;
                var parseResult = ParseResponse(response.Content);

                var step = new AgentStep
                {
                    Thought = parseResult.Thought,
                    Action = parseResult.Action,
                    ActionInput = parseResult.ActionInput
                };

                // 检查最终答案
                if (parseResult.IsFinalAnswer)
                {
                    TransitionTo(AgentState.Completing, "生成最终答案");
                    steps.Add(step);

                    TransitionTo(AgentState.Completed, "执行完成");

                    return new AgentResponse
                    {
                        Output = parseResult.FinalAnswer ?? parseResult.Thought,
                        IsSuccess = true,
                        Steps = steps,
                        TotalTokens = totalTokens
                    };
                }

                // 行动：执行工具
                if (!string.IsNullOrEmpty(parseResult.Action))
                {
                    TransitionTo(AgentState.Acting, $"执行工具：{parseResult.Action}");

                    var observation = await ExecuteToolAsync(
                        parseResult.Action,
                        parseResult.ActionInput ?? "",
                        context.Tools,
                        cancellationToken);

                    // 观察：处理结果
                    TransitionTo(AgentState.Observing, "处理工具结果");

                    step = step with { Observation = observation };

                    scratchpad.AppendLine($"思考：{parseResult.Thought}");
                    scratchpad.AppendLine($"行动：{parseResult.Action}");
                    scratchpad.AppendLine($"行动输入：{parseResult.ActionInput}");
                    scratchpad.AppendLine($"观察：{observation}");
                }

                steps.Add(step);
            }

            TransitionTo(AgentState.Error, "达到最大迭代次数");

            return new AgentResponse
            {
                Output = "达到最大迭代次数但未找到答案。",
                IsSuccess = false,
                Steps = steps,
                TotalTokens = totalTokens,
                Error = "超过最大迭代次数"
            };
        }
        catch (OperationCanceledException)
        {
            TransitionTo(AgentState.Cancelled, "执行被取消");
            throw;
        }
        catch (Exception ex)
        {
            TransitionTo(AgentState.Error, ex.Message);
            throw;
        }
    }

    private void TransitionTo(AgentState newState, string? message = null)
    {
        AgentState previousState;
        
        lock (_stateLock)
        {
            if (!IsValidTransition(_state, newState))
            {
                Logger.LogWarning(
                    "无效的状态转换：从 {PreviousState} 到 {NewState}",
                    _state, newState);
                return;
            }

            previousState = _state;
            _state = newState;
        }

        Logger.LogDebug(
            "Agent 状态变化：{PreviousState} -> {NewState}（{Message}）",
            previousState, newState, message);

        StateChanged?.Invoke(this, new AgentStateChangedEvent
        {
            PreviousState = previousState,
            CurrentState = newState,
            Message = message
        });
    }

    private static bool IsValidTransition(AgentState from, AgentState to)
    {
        // 定义有效的状态转换
        return (from, to) switch
        {
            (AgentState.Idle, AgentState.Thinking) => true,
            (AgentState.Thinking, AgentState.Acting) => true,
            (AgentState.Thinking, AgentState.Completing) => true,
            (AgentState.Thinking, AgentState.Error) => true,
            (AgentState.Acting, AgentState.Observing) => true,
            (AgentState.Acting, AgentState.Error) => true,
            (AgentState.Observing, AgentState.Thinking) => true,
            (AgentState.Observing, AgentState.Error) => true,
            (AgentState.Completing, AgentState.Completed) => true,
            (AgentState.Completing, AgentState.Error) => true,
            (_, AgentState.Cancelled) => true,
            (_, AgentState.Error) => true,
            _ => false
        };
    }

    // ... BuildPrompt 和 ParseResponse 方法与 ReActAgent 相同
    private string BuildPrompt(AgentContext context, string scratchpad) => "";
    private ParsedResponse ParseResponse(string response) => new();
    private record ParsedResponse
    {
        public string Thought { get; set; } = "";
        public string? Action { get; set; }
        public string? ActionInput { get; set; }
        public bool IsFinalAnswer { get; set; }
        public string? FinalAnswer { get; set; }
    }
}
```

### 4. 状态持久化

```csharp
namespace Dawning.Agents.Core.Agents;

/// <summary>
/// 持久化 Agent 状态的接口
/// </summary>
public interface IAgentStateStore
{
    /// <summary>
    /// 保存 Agent 状态
    /// </summary>
    Task SaveStateAsync(string agentId, AgentStateSnapshot snapshot, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 加载 Agent 状态
    /// </summary>
    Task<AgentStateSnapshot?> LoadStateAsync(string agentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除 Agent 状态
    /// </summary>
    Task DeleteStateAsync(string agentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 用于持久化的 Agent 状态快照
/// </summary>
public record AgentStateSnapshot
{
    public required string AgentId { get; init; }
    public required AgentState State { get; init; }
    public required IReadOnlyList<AgentStep> Steps { get; init; }
    public required IReadOnlyList<ConversationMessage> Messages { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    public IDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// 状态存储的内存实现
/// </summary>
public class InMemoryAgentStateStore : IAgentStateStore
{
    private readonly ConcurrentDictionary<string, AgentStateSnapshot> _states = new();

    public Task SaveStateAsync(string agentId, AgentStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var updated = snapshot with { UpdatedAt = DateTime.UtcNow };
        _states.AddOrUpdate(agentId, updated, (_, _) => updated);
        return Task.CompletedTask;
    }

    public Task<AgentStateSnapshot?> LoadStateAsync(string agentId, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(agentId, out var snapshot);
        return Task.FromResult(snapshot);
    }

    public Task DeleteStateAsync(string agentId, CancellationToken cancellationToken = default)
    {
        _states.TryRemove(agentId, out _);
        return Task.CompletedTask;
    }
}
```

---

## 总结

### Week 4 产出物

```text
src/Dawning.Agents.Core/
├── Memory/
│   ├── IConversationMemory.cs    # 记忆接口
│   ├── ConversationMessage.cs    # 消息模型
│   ├── BufferMemory.cs           # 简单缓冲记忆
│   ├── WindowMemory.cs           # 滑动窗口记忆
│   └── SummaryMemory.cs          # 摘要记忆
├── Tokens/
│   ├── ITokenCounter.cs          # Token 计数接口
│   ├── TiktokenCounter.cs        # 基于 Tiktoken 的计数器
│   └── SimpleTokenCounter.cs     # 基于估算的计数器
└── Agents/
    ├── AgentState.cs             # 状态定义
    ├── StatefulAgent.cs          # 有状态 Agent 实现
    └── IAgentStateStore.cs       # 状态持久化接口
```

### 学到的关键概念

| 概念 | 描述 |
|------|------|
| **记忆类型** | 缓冲、窗口、摘要 |
| **Token 计数** | Tiktoken、估算方法 |
| **上下文管理** | 裁剪、摘要 |
| **状态机** | 状态转换、持久化 |

### 集成模式

```csharp
// 完整的 Agent 设置，包含记忆和状态管理
var tokenCounter = new TiktokenCounter("gpt-4");
var memory = new SummaryMemory(llmProvider, tokenCounter);

var agent = new StatefulAgent(llmProvider, logger);
agent.StateChanged += (sender, e) => 
    Console.WriteLine($"状态：{e.CurrentState}");

var context = new AgentContext
{
    Input = "今天天气怎么样？",
    Memory = memory,
    Tools = [weatherTool],
    MaxIterations = 10
};

var response = await agent.ExecuteAsync(context);
```

### 下一步：Week 5-6（Phase 3）

Phase 3 将涵盖：
- 工具开发与集成
- 工具结果解析
- 错误处理和重试
