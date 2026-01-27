---
name: dotnet-patterns
description: .NET patterns and best practices for Dawning.Agents project
---

# .NET Patterns Skill

Apply .NET best practices and patterns specific to the Dawning.Agents project.

## When to Use

- When implementing new features
- When refactoring existing code
- When asking "what's the best way to..."
- When checking code quality

## Core Patterns

### 1. Dependency Injection (Pure DI)

```csharp
// ✅ Always use DI
var provider = serviceProvider.GetRequiredService<ILLMProvider>();

// ❌ Never use new directly for services
var provider = new OllamaProvider("model");
```

### 2. Logger with NullLogger Fallback

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService>? logger = null)
    {
        _logger = logger ?? NullLogger<MyService>.Instance;
    }
}
```

### 3. CancellationToken on All Async Methods

```csharp
public interface IMyService
{
    Task<Result> DoAsync(
        Input input,
        CancellationToken cancellationToken = default);
}
```

### 4. Options Pattern for Configuration

```csharp
// Options class
public class MyOptions
{
    public const string SectionName = "My";
    public string Value { get; set; } = "default";
}

// Registration
services.Configure<MyOptions>(
    configuration.GetSection(MyOptions.SectionName));

// Usage
public class MyService
{
    private readonly MyOptions _options;

    public MyService(IOptions<MyOptions> options)
    {
        _options = options.Value;
    }
}
```

### 5. Interface Segregation

```
Abstractions/     → Interfaces only, zero dependencies
├── IMyService.cs
├── MyOptions.cs
└── MyModels.cs

Core/             → Implementations
├── MyService.cs
└── MyServiceExtensions.cs
```

## CSharpier Formatting Rules

### Long Parameter Lists

```csharp
// ✅ Each parameter on its own line
public MyService(
    ILLMProvider llmProvider,
    IOptions<MyOptions> options,
    ILogger<MyService>? logger = null
)
{
}
```

### Collection Initializers

```csharp
// ✅ Elements on separate lines with trailing comma
var list = new List<string>
{
    "item1",
    "item2",
    "item3",
};
```

### Method Chains

```csharp
// ✅ Each call on its own line
var result = items
    .Where(x => x.IsActive)
    .Select(x => x.Name)
    .ToList();
```

### Always Use Braces

```csharp
// ✅ Always use braces
if (condition)
{
    DoSomething();
}

// ❌ Never skip braces
if (condition)
    DoSomething();
```

## XML Documentation

```csharp
/// <summary>
/// Brief description of the class/method.
/// </summary>
/// <param name="input">Description of input parameter.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Description of return value.</returns>
/// <exception cref="ArgumentNullException">When input is null.</exception>
public async Task<Result> ProcessAsync(
    Input input,
    CancellationToken cancellationToken = default)
{
}
```

## Tech Stack Reference

| Category | Technology |
|----------|------------|
| Framework | .NET 10.0 |
| Local LLM | Ollama |
| Remote LLM | OpenAI, Azure OpenAI |
| Testing | xUnit |
| Assertions | FluentAssertions |
| Mocking | Moq |
| Formatting | CSharpier |
