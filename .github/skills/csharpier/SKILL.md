---
name: csharpier
description: "CSharpier code formatting rules and conventions for Dawning.Agents. Use when writing or reviewing C# code. Trigger phrases: format code, 格式化代码, csharpier, code style, 代码风格"
---

# CSharpier Formatting Skill

## What This Skill Does

Defines the authoritative code formatting rules using CSharpier. All C# code in this project must follow these conventions.

## When to Use

- Writing new C# code
- Reviewing code for formatting issues
- Refactoring existing code
- When asked about code style or formatting

## Core Rules

### 1. Long Parameter Lists - One Per Line

```csharp
// ✅ Correct - each parameter on its own line
public MyService(
    ILLMProvider llmProvider,
    IOptions<MyOptions> options,
    ILogger<MyService>? logger = null
)
{
    _llmProvider = llmProvider;
    _options = options.Value;
    _logger = logger ?? NullLogger<MyService>.Instance;
}

// ❌ Wrong - single line too long
public MyService(ILLMProvider llmProvider, IOptions<MyOptions> options, ILogger<MyService>? logger = null)
{
}
```

### 2. Collection Initializers - Elements on Separate Lines

```csharp
// ✅ Correct - elements on separate lines with trailing comma
var messages = new List<ChatMessage>
{
    new("system", systemPrompt),
    new("user", userInput),
};

var options = new AgentOptions
{
    MaxIterations = 10,
    TimeoutSeconds = 30,
    EnableLogging = true,
};

// ❌ Wrong - all on one line
var messages = new List<ChatMessage> { new("system", systemPrompt), new("user", userInput) };
```

### 3. Method Chaining - Each Call on Its Own Line

```csharp
// ✅ Correct - each method call on separate line
var result = items
    .Where(x => x.IsActive)
    .OrderBy(x => x.Name)
    .Select(x => x.ToDto())
    .ToList();

var services = new ServiceCollection()
    .AddSingleton<IMyService, MyService>()
    .AddScoped<IRepository, Repository>()
    .BuildServiceProvider();

// ❌ Wrong - chained on single line
var result = items.Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => x.ToDto()).ToList();
```

### 4. If Statements - Always Use Braces

```csharp
// ✅ Correct - always use braces
if (condition)
{
    DoSomething();
}

if (value == null)
{
    return;
}

// ❌ Wrong - no braces
if (condition)
    DoSomething();

if (value == null) return;
```

### 5. Long Method Calls - Arguments on Separate Lines

```csharp
// ✅ Correct - arguments on separate lines
await _llmProvider.ChatAsync(
    messages,
    new LLMOptions
    {
        Temperature = 0.7f,
        MaxTokens = 1000,
    },
    cancellationToken
);

_logger.LogInformation(
    "Processing request {RequestId} for user {UserId}",
    requestId,
    userId
);

// ❌ Wrong - all on one line
await _llmProvider.ChatAsync(messages, new LLMOptions { Temperature = 0.7f, MaxTokens = 1000 }, cancellationToken);
```

### 6. Lambda Expressions - Multi-line for Complex Bodies

```csharp
// ✅ Correct - simple lambda inline
var names = items.Select(x => x.Name);

// ✅ Correct - complex lambda multi-line
var processed = items.Select(x =>
{
    var result = ProcessItem(x);
    return new ProcessedItem
    {
        Id = x.Id,
        Result = result,
    };
});

// ❌ Wrong - complex logic on single line
var processed = items.Select(x => { var result = ProcessItem(x); return new ProcessedItem { Id = x.Id, Result = result }; });
```

### 7. String Interpolation - Break Long Strings

```csharp
// ✅ Correct - break at logical points
var message = $"User {userId} performed action {actionName} " +
    $"on resource {resourceId} at {timestamp:O}";

// ✅ Correct - use raw string literals for multi-line
var json = """
    {
        "name": "test",
        "value": 123
    }
    """;

// ❌ Wrong - very long single line
var message = $"User {userId} performed action {actionName} on resource {resourceId} at {timestamp:O} with result {result}";
```

### 8. Switch Expressions - Cases on Separate Lines

```csharp
// ✅ Correct - each case on its own line
var result = status switch
{
    Status.Active => "运行中",
    Status.Paused => "已暂停",
    Status.Stopped => "已停止",
    _ => "未知",
};

// ❌ Wrong - all on one line
var result = status switch { Status.Active => "运行中", Status.Paused => "已暂停", _ => "未知" };
```

### 9. Attribute Lists - One Per Line for Multiple

```csharp
// ✅ Correct - one attribute per line
[FunctionTool("Tool description")]
[Category("Utilities")]
[RequiresConfirmation]
public async Task<ToolResult> MyToolAsync(string input)
{
}

// ✅ Correct - single attribute inline
[Fact]
public void TestMethod()
{
}
```

### 10. Trailing Commas - Always Include

```csharp
// ✅ Correct - trailing commas in multi-line collections
var list = new[]
{
    "item1",
    "item2",
    "item3",  // trailing comma
};

new MyClass
{
    Property1 = "value1",
    Property2 = "value2",  // trailing comma
};
```

## Running CSharpier

### Check Installation

```bash
dotnet tool list -g | grep csharpier
```

### Install CSharpier

```bash
dotnet tool install -g csharpier
```

### Format Files

```bash
# Format single file
dotnet-csharpier path/to/file.cs

# Format directory
dotnet-csharpier src/

# Check only (no changes)
dotnet-csharpier --check src/
```

### IDE Integration

- **VS Code**: Install "CSharpier" extension, enable "Format on Save"
- **Rider**: CSharpier plugin available
- **Visual Studio**: CSharpier extension available

## CSharpierTool Usage

The project includes a built-in `CSharpierTool` that agents can use:

```csharp
// Register the tool
services.AddCSharpierTools();

// Available methods
- FormatFile(filePath, checkOnly)     // Format single file
- FormatDirectory(path, checkOnly)    // Format all .cs files
- FormatCode(code)                    // Format code string
- CheckInstallation()                 // Verify CSharpier installed
- Install()                           // Install CSharpier (requires confirmation)
- GetFormattingRules()                // Get this formatting guide
```

## Quick Reference

| Pattern | Rule |
|---------|------|
| Parameters | One per line when multiple |
| Collections | Elements on separate lines, trailing comma |
| Method chains | One call per line |
| If statements | Always use braces |
| Long calls | Arguments on separate lines |
| Switch expressions | Cases on separate lines |
| Attributes | One per line when multiple |
