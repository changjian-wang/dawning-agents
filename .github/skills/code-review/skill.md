---
name: code-review
description: Review .NET code for Dawning.Agents project following established patterns and best practices
---

# Code Review Skill

Review code changes for the Dawning.Agents project, ensuring they follow the established patterns and best practices.

## When to Use

- When asked to review code, PR, or changes
- When asked to check code quality
- When asked "is this code good?"

## Review Checklist

### 1. Architecture Compliance

- [ ] Uses DI (Dependency Injection) - no `new` for services
- [ ] Interfaces in `Abstractions/`, implementations in `Core/`
- [ ] Supports `ILogger<T>` with `NullLogger` fallback
- [ ] Supports `CancellationToken` on all async methods
- [ ] Uses `IOptions<T>` for configuration

### 2. API Design (Minimal API Principle)

```csharp
// ✅ Good - Simple, one-line registration
services.AddLLMProvider(configuration);

// ❌ Avoid - Over-engineered Builder pattern
services.AddLLMProvider(builder => builder.UseFactory(...).WithRetry(...));
```

### 3. Code Style (CSharpier)

- Long parameter lists: one parameter per line
- Collection initializers: elements on separate lines with trailing comma
- Method chains: one call per line
- Always use braces for `if` statements

```csharp
// ✅ Good
public MyService(
    ILLMProvider llmProvider,
    IOptions<MyOptions> options,
    ILogger<MyService>? logger = null
)
{
}

// ✅ Good
var messages = new List<ChatMessage>
{
    new("system", systemPrompt),
    new("user", userInput),
};
```

### 4. Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Interface | `I` prefix | `ILLMProvider`, `IAgent` |
| Config class | `Options` suffix | `LLMOptions`, `AgentOptions` |
| DI extension | `Add` prefix | `AddLLMProvider`, `AddAgent` |
| Async method | `Async` suffix | `ChatAsync`, `RunAsync` |
| Stream method | `StreamAsync` suffix | `ChatStreamAsync` |

### 5. Required Elements

- [ ] XML documentation comments on public APIs
- [ ] Unit tests for new functionality
- [ ] No hardcoded configuration values

## Review Output Format

```
## Code Review Summary

### ✅ Good
- [list positive aspects]

### ⚠️ Suggestions
- [list suggestions with examples]

### ❌ Issues
- [list issues that must be fixed]

### Overall: [Pass/Pass with suggestions/Needs changes]
```
