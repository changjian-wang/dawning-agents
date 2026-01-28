---
name: csharpier
description: >
  CSharpier code formatting rules and conventions for Dawning.Agents.
  Use when writing or reviewing C# code to ensure consistent formatting.
  CSharpier is the authoritative formatter - all code must follow these rules.
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

---

## Markdown Formatting Rules

### 1. Headings - Blank Lines Before and After

```markdown
<!-- ✅ Correct -->
Some text here.

## Heading

More text here.

<!-- ❌ Wrong - no blank lines -->
Some text here.
## Heading
More text here.
```

### 2. Code Blocks - Specify Language

````markdown
<!-- ✅ Correct - with language -->
```csharp
var x = 1;
```

```bash
dotnet build
```

```json
{ "key": "value" }
```

<!-- ❌ Wrong - no language -->
```
var x = 1;
```
````

### 3. Lists - Consistent Markers

```markdown
<!-- ✅ Correct - consistent markers -->
- Item 1
- Item 2
- Item 3

1. First
2. Second
3. Third

<!-- ❌ Wrong - mixed markers -->
- Item 1
* Item 2
+ Item 3
```

### 4. Lists - Blank Line for Nested Content

```markdown
<!-- ✅ Correct - blank line before nested code -->
- Item with code:

  ```csharp
  var x = 1;
  ```

- Next item

<!-- ❌ Wrong - no blank line -->
- Item with code:
  ```csharp
  var x = 1;
  ```
```

### 5. Tables - Aligned Columns

```markdown
<!-- ✅ Correct - aligned -->
| Name   | Type   | Description       |
|--------|--------|-------------------|
| id     | Guid   | Unique identifier |
| name   | string | Display name      |

<!-- ❌ Wrong - not aligned -->
| Name | Type | Description |
|---|---|---|
| id | Guid | Unique identifier |
```

### 6. Links - Descriptive Text

```markdown
<!-- ✅ Correct - descriptive -->
See the [configuration guide](docs/config.md) for details.
Check out [Microsoft Docs](https://docs.microsoft.com).

<!-- ❌ Wrong - generic text -->
Click [here](docs/config.md) for details.
See [https://docs.microsoft.com](https://docs.microsoft.com).
```

### 7. Emphasis - Use Sparingly

```markdown
<!-- ✅ Correct - meaningful emphasis -->
This is **important** information.
Use `code` for inline code references.

<!-- ❌ Wrong - overused -->
This is **very** **important** **information**.
```

### 8. Line Length - Wrap at ~100 Characters

```markdown
<!-- ✅ Correct - wrapped -->
This is a long paragraph that should be wrapped at a reasonable
length to maintain readability in plain text editors.

<!-- ❌ Wrong - very long line -->
This is a long paragraph that goes on and on without any line breaks which makes it very hard to read in plain text editors and causes horizontal scrolling.
```

### 9. XML Documentation Comments

```csharp
// ✅ Correct - complete documentation
/// <summary>
/// Gets user by ID.
/// </summary>
/// <param name="id">The user's unique identifier.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The user DTO or null if not found.</returns>
/// <exception cref="ArgumentException">Thrown when id is empty.</exception>
public async Task<UserDto?> GetByIdAsync(
    Guid id,
    CancellationToken cancellationToken = default
);

// ❌ Wrong - incomplete or missing
public async Task<UserDto?> GetByIdAsync(Guid id);
```

### 10. README Structure

```markdown
# Project Name

Brief description of the project.

## Features

- Feature 1
- Feature 2

## Installation

```bash
dotnet add package ProjectName
```

## Quick Start

```csharp
// Example code
```

## Configuration

Configuration details...

## API Reference

API documentation...

## License

MIT License
```

## Markdown Quick Reference

| Element | Rule |
|---------|------|
| Headings | Blank lines before/after |
| Code blocks | Always specify language |
| Lists | Consistent markers (- or 1.) |
| Nested content | Blank line + 2-space indent |
| Tables | Align columns |
| Links | Descriptive text, not "click here" |
| Emphasis | Use sparingly |
| Line length | Wrap at ~100 characters |
| XML docs | Complete with summary, params, returns |
