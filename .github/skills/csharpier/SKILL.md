---
description: |
  Use when: Formatting C# code with CSharpier, checking formatting rules, or fixing style violations
  Don't use when:
    - Reviewing code logic (use code-review)
    - Writing new code (use code-update)
    - Fixing compilation errors (use build-project)
    - Running tests (use run-tests)
  Inputs: Request to format code or fix style issues
  Outputs: Formatted code via `dotnet csharpier format .`
  Success criteria: CSharpier reports 0 files changed (all code already formatted)
---

# CSharpier Formatting Skill

## Core Rules

### 1. Long Parameter Lists — One Per Line

```csharp
// ✅ Correct
public MyService(
    ILLMProvider llmProvider,
    IOptions<MyOptions> options,
    ILogger<MyService>? logger = null
)

// ❌ Wrong — single line too long
public MyService(ILLMProvider llmProvider, IOptions<MyOptions> options, ILogger<MyService>? logger = null)
```

### 2. Collection Initializers — Elements on Separate Lines

```csharp
// ✅ Correct — trailing comma
var messages = new List<ChatMessage>
{
    new("system", systemPrompt),
    new("user", userInput),
};
```

### 3. Method Chaining — Each Call on Its Own Line

```csharp
var result = items
    .Where(x => x.IsActive)
    .OrderBy(x => x.Name)
    .Select(x => x.ToDto())
    .ToList();
```

### 4. If Statements — Always Use Braces

```csharp
// ✅ Always use braces
if (condition)
{
    DoSomething();
}
```

### 5. Long Method Calls — Arguments on Separate Lines

```csharp
await _llmProvider.ChatAsync(
    messages,
    new LLMOptions { Temperature = 0.7f },
    cancellationToken
);
```

### 6-10. Additional Rules

- **Lambda**: multi-line for complex bodies
- **String Interpolation**: break long strings
- **Switch Expressions**: cases on separate lines
- **Attributes**: one per line when multiple
- **Trailing Commas**: always include in multi-line collections

## Running CSharpier

```bash
# Format all
~/.dotnet/tools/csharpier format .

# Check only
~/.dotnet/tools/csharpier --check src/

# Install
dotnet tool install -g csharpier
```

