# Memory Systems

Memory allows agents to maintain context across conversations.

## Memory Types

| Type | Description | Use Case |
|------|-------------|----------|
| `BufferMemory` | Stores all messages | Short conversations |
| `WindowMemory` | Keeps last N messages | Long conversations |
| `SummaryMemory` | Summarizes old messages | Very long conversations |

## Registration

```csharp
// Auto-select based on configuration
services.AddMemory(configuration);

// Or specific type
services.AddBufferMemory();
services.AddWindowMemory(windowSize: 10);
services.AddSummaryMemory();
```

## Configuration

```json
{
  "Memory": {
    "Type": "Window",
    "WindowSize": 20,
    "MaxTokens": 4000
  }
}
```

## Usage

```csharp
var memory = serviceProvider.GetRequiredService<IConversationMemory>();

// Add messages
await memory.AddMessageAsync(new ConversationMessage("user", "Hello"));
await memory.AddMessageAsync(new ConversationMessage("assistant", "Hi!"));

// Get context for LLM
var context = await memory.GetContextAsync(maxTokens: 4000);

// Get all messages
var messages = await memory.GetMessagesAsync();

// Clear memory
await memory.ClearAsync();
```

## Token Counting

```csharp
services.AddTokenCounter();

var counter = serviceProvider.GetRequiredService<ITokenCounter>();
var tokens = counter.CountTokens("Hello world");
var messageTokens = counter.CountTokens(messages);
```

## Buffer Memory

Stores all messages without any pruning.

```csharp
services.AddBufferMemory();
```

Best for:
- Short conversations
- When you need complete history
- Debugging

## Window Memory

Keeps only the most recent N messages.

```csharp
services.AddWindowMemory(windowSize: 10);
```

Best for:
- Long-running conversations
- Consistent memory usage
- Most production use cases

## Summary Memory

Automatically summarizes older messages to stay within token limits.

```csharp
services.AddSummaryMemory();
```

Configuration:

```json
{
  "Memory": {
    "Type": "Summary",
    "MaxTokens": 4000,
    "SummaryPrompt": "Summarize the following conversation:"
  }
}
```

Best for:
- Very long conversations
- When context is important
- Complex multi-turn interactions

## Integration with Agent

Memory is automatically integrated with agents:

```csharp
services.AddAgent<ReActAgent>();
services.AddWindowMemory(windowSize: 20);

var agent = serviceProvider.GetRequiredService<IAgent>();

// Memory is used automatically
await agent.RunAsync("What's 2+2?");
await agent.RunAsync("And multiply that by 3?"); // Remembers previous result
```
