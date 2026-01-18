# Week 4: Conversation Management & Memory

> Phase 2: Single Agent Development Core Skills
> Week 4 Learning Material: Managing Conversations, Memory, and Agent State

---

## Day 1-2: Conversation History Management

### 1. Why Memory Matters

Agents need memory to:
- **Maintain context** across multi-turn conversations
- **Remember user preferences** and prior decisions
- **Track task progress** over time
- **Avoid repetition** and inconsistency

```text
┌─────────────────────────────────────────────────────────────────┐
│                    Memory Types in Agents                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────┐  ┌──────────────────┐  ┌────────────────┐ │
│  │  Short-term      │  │  Long-term       │  │  Working       │ │
│  │  Memory          │  │  Memory          │  │  Memory        │ │
│  ├──────────────────┤  ├──────────────────┤  ├────────────────┤ │
│  │ • Current conv.  │  │ • User profile   │  │ • Current task │ │
│  │ • Recent context │  │ • Past sessions  │  │ • Active goals │ │
│  │ • Buffer-based   │  │ • Vector DB      │  │ • Scratchpad   │ │
│  └──────────────────┘  └──────────────────┘  └────────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Memory Interface Design

```csharp
namespace Dawning.Agents.Core.Memory;

/// <summary>
/// Represents a message in conversation history
/// </summary>
public record ConversationMessage
{
    /// <summary>
    /// Unique identifier for the message
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Role: "user", "assistant", or "system"
    /// </summary>
    public required string Role { get; init; }
    
    /// <summary>
    /// Message content
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// When the message was created
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Optional metadata (e.g., tool calls, token count)
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }
    
    /// <summary>
    /// Estimated token count for this message
    /// </summary>
    public int? TokenCount { get; init; }
}

/// <summary>
/// Interface for conversation memory management
/// </summary>
public interface IConversationMemory
{
    /// <summary>
    /// Add a message to memory
    /// </summary>
    Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all messages in memory
    /// </summary>
    Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get messages formatted for LLM context
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear all messages from memory
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the current token count
    /// </summary>
    Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default);
}
```

### 3. Buffer Memory Implementation

The simplest memory type - stores all messages in a list:

```csharp
namespace Dawning.Agents.Core.Memory;

using Dawning.Agents.Core.LLM;

/// <summary>
/// Simple buffer memory that stores all messages
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
            // Calculate token count if not provided
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
                // Take most recent messages that fit within token limit
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
        
        // Start from most recent messages
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

### 4. Window Memory Implementation

Keeps only the last N messages:

```csharp
namespace Dawning.Agents.Core.Memory;

/// <summary>
/// Memory that keeps only the last N messages
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
        _windowSize = windowSize > 0 ? windowSize : throw new ArgumentException("Window size must be positive");
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
            
            // Trim to window size
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

## Day 3-4: Token Management

### 1. Understanding Token Counting

Tokens are the fundamental units that LLMs process. Managing tokens is crucial because:
- LLMs have **context window limits** (4K, 8K, 128K, etc.)
- **Cost** is calculated per token
- **Performance** degrades with excessive context

#### Token Estimation Rules

| Content Type | Approximate Ratio |
|-------------|-------------------|
| English text | ~4 characters per token |
| Chinese text | ~1-2 characters per token |
| Code | ~3-4 characters per token |
| Whitespace | Varies |

### 2. Token Counter Interface

```csharp
namespace Dawning.Agents.Core.Tokens;

/// <summary>
/// Interface for counting tokens in text
/// </summary>
public interface ITokenCounter
{
    /// <summary>
    /// Count the number of tokens in the given text
    /// </summary>
    int CountTokens(string text);
    
    /// <summary>
    /// Count tokens for a list of messages (includes role overhead)
    /// </summary>
    int CountTokens(IEnumerable<ChatMessage> messages);
    
    /// <summary>
    /// Get the model name this counter is for
    /// </summary>
    string ModelName { get; }
    
    /// <summary>
    /// Get the maximum context window size
    /// </summary>
    int MaxContextTokens { get; }
}
```

### 3. Tiktoken-based Implementation

Using the SharpToken library (C# port of tiktoken):

```csharp
namespace Dawning.Agents.Core.Tokens;

using SharpToken;

/// <summary>
/// Token counter using tiktoken encoding
/// </summary>
public class TiktokenCounter : ITokenCounter
{
    private readonly GptEncoding _encoding;
    private readonly int _tokensPerMessage;
    private readonly int _tokensPerName;

    public string ModelName { get; }
    public int MaxContextTokens { get; }

    // Model configurations
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
        
        // Get encoding for model
        _encoding = GptEncoding.GetEncodingForModel(modelName);
        
        // Get model configuration
        if (ModelConfigs.TryGetValue(modelName, out var config))
        {
            MaxContextTokens = config.maxTokens;
            _tokensPerMessage = config.perMessage;
            _tokensPerName = config.perName;
        }
        else
        {
            // Default to GPT-4 settings
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
        
        totalTokens += 3; // Every reply is primed with <|start|>assistant<|message|>
        
        return totalTokens;
    }
}
```

### 4. Simple Estimation Counter

For scenarios where tiktoken is not available:

```csharp
namespace Dawning.Agents.Core.Tokens;

/// <summary>
/// Simple token counter using character-based estimation
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
        
        return total + 3; // Reply priming
    }

    private static bool IsChinese(char c)
    {
        // CJK Unified Ideographs range
        return c >= 0x4E00 && c <= 0x9FFF;
    }
}
```

### 5. Summary Memory with Compression

```csharp
namespace Dawning.Agents.Core.Memory;

using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Tokens;

/// <summary>
/// Memory that summarizes older messages to save tokens
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

            // Check if we need to summarize
            if (_recentMessages.Count <= _maxRecentMessages)
            {
                return;
            }

            // Take oldest messages to summarize (keep most recent)
            var toSummarize = _recentMessages.Count - _maxRecentMessages / 2;
            messagesToSummarize = _recentMessages.Take(toSummarize).ToList();
            _recentMessages.RemoveRange(0, toSummarize);
        }

        // Summarize outside the lock
        await SummarizeMessagesAsync(messagesToSummarize, cancellationToken);
    }

    private async Task SummarizeMessagesAsync(
        List<ConversationMessage> messages,
        CancellationToken cancellationToken)
    {
        var conversationText = string.Join("\n", messages.Select(m => $"{m.Role}: {m.Content}"));
        
        var prompt = $"""
            Summarize the following conversation, preserving key information and context:
            
            Previous Summary: {(string.IsNullOrEmpty(_summary) ? "None" : _summary)}
            
            New Messages:
            {conversationText}
            
            Provide a concise summary that captures:
            1. Main topics discussed
            2. Key decisions or conclusions
            3. Important context for future conversation
            
            Summary:
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
            
            // Add summary as system message if exists
            if (!string.IsNullOrEmpty(_summary))
            {
                result.Add(new ConversationMessage
                {
                    Role = "system",
                    Content = $"Previous conversation summary: {_summary}"
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

## Day 5-7: Agent State Machine

### 1. Understanding Agent States

Agents go through various states during execution:

```text
┌─────────────────────────────────────────────────────────────────┐
│                    Agent State Machine                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│    ┌────────┐                                                    │
│    │  Idle  │◄───────────────────────────────────┐               │
│    └───┬────┘                                    │               │
│        │ Start                                   │ Complete/     │
│        ▼                                         │ Error         │
│    ┌────────────┐                                │               │
│    │ Thinking   │◄──────────┐                    │               │
│    └───┬────────┘           │                    │               │
│        │ Decide             │                    │               │
│        ▼                    │                    │               │
│    ┌────────────┐     ┌─────┴──────┐             │               │
│    │  Acting    │────►│ Observing  │             │               │
│    └───┬────────┘     └────────────┘             │               │
│        │                                         │               │
│        │ Finish                                  │               │
│        ▼                                         │               │
│    ┌────────────┐                                │               │
│    │ Completing │────────────────────────────────┘               │
│    └────────────┘                                                │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. State Definitions

```csharp
namespace Dawning.Agents.Core.Agents;

/// <summary>
/// Represents the current state of an agent
/// </summary>
public enum AgentState
{
    /// <summary>
    /// Agent is idle, waiting for input
    /// </summary>
    Idle,
    
    /// <summary>
    /// Agent is processing input and deciding what to do
    /// </summary>
    Thinking,
    
    /// <summary>
    /// Agent is executing an action (tool call)
    /// </summary>
    Acting,
    
    /// <summary>
    /// Agent is processing the result of an action
    /// </summary>
    Observing,
    
    /// <summary>
    /// Agent is generating the final response
    /// </summary>
    Completing,
    
    /// <summary>
    /// Agent has finished executing
    /// </summary>
    Completed,
    
    /// <summary>
    /// Agent encountered an error
    /// </summary>
    Error,
    
    /// <summary>
    /// Agent is waiting for external input
    /// </summary>
    WaitingForInput,
    
    /// <summary>
    /// Agent execution was cancelled
    /// </summary>
    Cancelled
}

/// <summary>
/// Event raised when agent state changes
/// </summary>
public record AgentStateChangedEvent
{
    public required AgentState PreviousState { get; init; }
    public required AgentState CurrentState { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? Message { get; init; }
}
```

### 3. Stateful Agent Implementation

```csharp
namespace Dawning.Agents.Core.Agents;

using Dawning.Agents.Core.LLM;
using Microsoft.Extensions.Logging;

/// <summary>
/// Agent with explicit state management
/// </summary>
public class StatefulAgent : AgentBase
{
    private AgentState _state = AgentState.Idle;
    private readonly object _stateLock = new();

    public override string Name { get; }
    public override string Description { get; }

    /// <summary>
    /// Current agent state
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
    /// Event raised when state changes
    /// </summary>
    public event EventHandler<AgentStateChangedEvent>? StateChanged;

    public StatefulAgent(
        ILLMProvider llm,
        ILogger<StatefulAgent> logger,
        string? name = null,
        string? description = null) : base(llm, logger)
    {
        Name = name ?? "StatefulAgent";
        Description = description ?? "An agent with explicit state management";
    }

    protected override string GetDefaultSystemPrompt()
    {
        return """
            You are a helpful AI assistant that solves problems step by step.
            Think carefully before acting, and explain your reasoning.
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
            TransitionTo(AgentState.Thinking, "Processing input");

            for (int iteration = 0; iteration < context.MaxIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // THINKING: Decide what to do
                TransitionTo(AgentState.Thinking, $"Iteration {iteration + 1}");

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

                // Check for final answer
                if (parseResult.IsFinalAnswer)
                {
                    TransitionTo(AgentState.Completing, "Generating final answer");
                    steps.Add(step);

                    TransitionTo(AgentState.Completed, "Execution complete");

                    return new AgentResponse
                    {
                        Output = parseResult.FinalAnswer ?? parseResult.Thought,
                        IsSuccess = true,
                        Steps = steps,
                        TotalTokens = totalTokens
                    };
                }

                // ACTING: Execute the tool
                if (!string.IsNullOrEmpty(parseResult.Action))
                {
                    TransitionTo(AgentState.Acting, $"Executing tool: {parseResult.Action}");

                    var observation = await ExecuteToolAsync(
                        parseResult.Action,
                        parseResult.ActionInput ?? "",
                        context.Tools,
                        cancellationToken);

                    // OBSERVING: Process the result
                    TransitionTo(AgentState.Observing, "Processing tool result");

                    step = step with { Observation = observation };

                    scratchpad.AppendLine($"Thought: {parseResult.Thought}");
                    scratchpad.AppendLine($"Action: {parseResult.Action}");
                    scratchpad.AppendLine($"Action Input: {parseResult.ActionInput}");
                    scratchpad.AppendLine($"Observation: {observation}");
                }

                steps.Add(step);
            }

            TransitionTo(AgentState.Error, "Maximum iterations reached");

            return new AgentResponse
            {
                Output = "Maximum iterations reached without finding an answer.",
                IsSuccess = false,
                Steps = steps,
                TotalTokens = totalTokens,
                Error = "Max iterations exceeded"
            };
        }
        catch (OperationCanceledException)
        {
            TransitionTo(AgentState.Cancelled, "Execution cancelled");
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
                    "Invalid state transition from {PreviousState} to {NewState}",
                    _state, newState);
                return;
            }

            previousState = _state;
            _state = newState;
        }

        Logger.LogDebug(
            "Agent state changed: {PreviousState} -> {NewState} ({Message})",
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
        // Define valid state transitions
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

    // ... BuildPrompt and ParseResponse methods same as ReActAgent
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

### 4. State Persistence

```csharp
namespace Dawning.Agents.Core.Agents;

/// <summary>
/// Interface for persisting agent state
/// </summary>
public interface IAgentStateStore
{
    /// <summary>
    /// Save agent state
    /// </summary>
    Task SaveStateAsync(string agentId, AgentStateSnapshot snapshot, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Load agent state
    /// </summary>
    Task<AgentStateSnapshot?> LoadStateAsync(string agentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete agent state
    /// </summary>
    Task DeleteStateAsync(string agentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Snapshot of agent state for persistence
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
/// In-memory implementation of state store
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

## Summary

### Week 4 Deliverables

```text
src/Dawning.Agents.Core/
├── Memory/
│   ├── IConversationMemory.cs    # Memory interface
│   ├── ConversationMessage.cs    # Message model
│   ├── BufferMemory.cs           # Simple buffer memory
│   ├── WindowMemory.cs           # Sliding window memory
│   └── SummaryMemory.cs          # Summarizing memory
├── Tokens/
│   ├── ITokenCounter.cs          # Token counting interface
│   ├── TiktokenCounter.cs        # Tiktoken-based counter
│   └── SimpleTokenCounter.cs     # Estimation-based counter
└── Agents/
    ├── AgentState.cs             # State definitions
    ├── StatefulAgent.cs          # Stateful agent implementation
    └── IAgentStateStore.cs       # State persistence interface
```

### Key Concepts Learned

| Concept | Description |
|---------|-------------|
| **Memory Types** | Buffer, Window, Summary |
| **Token Counting** | Tiktoken, estimation methods |
| **Context Management** | Trimming, summarization |
| **State Machine** | State transitions, persistence |

### Integration Pattern

```csharp
// Complete agent setup with memory and state management
var tokenCounter = new TiktokenCounter("gpt-4");
var memory = new SummaryMemory(llmProvider, tokenCounter);

var agent = new StatefulAgent(llmProvider, logger);
agent.StateChanged += (sender, e) => 
    Console.WriteLine($"State: {e.CurrentState}");

var context = new AgentContext
{
    Input = "What's the weather like?",
    Memory = memory,
    Tools = [weatherTool],
    MaxIterations = 10
};

var response = await agent.ExecuteAsync(context);
```

### Next: Week 5-6 (Phase 3)

Phase 3 will cover:
- Tool development and integration
- Tool result parsing
- Error handling and retries
