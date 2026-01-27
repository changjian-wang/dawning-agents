---
name: dotnet-patterns
description: >
  .NET patterns and best practices for Dawning.Agents: DI, logging, async,
  options pattern, and CSharpier formatting. Use when asking "what's the best 
  way to...", "how should I implement...", or "is this the right pattern?".
---

# .NET Patterns Skill

## What This Skill Does

Provides .NET best practices and patterns specific to the Dawning.Agents project.

## When to Use

- "What's the best way to..."
- "How should I implement..."
- "Is this the right pattern?"
- "Show me the correct way to..."
- When implementing new features

## Core Patterns

### 1. Pure Dependency Injection

```csharp
// ✅ Always use DI
public class MyService
{
    private readonly ILLMProvider _provider;
    
    public MyService(ILLMProvider provider)
    {
        _provider = provider;
    }
}

// Usage
var service = serviceProvider.GetRequiredService<IMyService>();

// ❌ Never instantiate services directly
var provider = new OllamaProvider("model"); // WRONG!
```

### 2. Logger with NullLogger Fallback

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    // Logger is optional with fallback
    public MyService(ILogger<MyService>? logger = null)
    {
        _logger = logger ?? NullLogger<MyService>.Instance;
    }

    public void DoWork()
    {
        _logger.LogDebug("Starting work...");
        // ...
        _logger.LogInformation("Work completed");
    }
}
```

### 3. CancellationToken on All Async Methods

```csharp
public interface IMyService
{
    // Always include CancellationToken with default value
    Task<Result> ProcessAsync(
        Input input,
        CancellationToken cancellationToken = default);
}

public class MyService : IMyService
{
    public async Task<Result> ProcessAsync(
        Input input,
        CancellationToken cancellationToken = default)
    {
        // Pass token to async operations
        await _httpClient.SendAsync(request, cancellationToken);
        
        // Check for cancellation in loops
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessItemAsync(item, cancellationToken);
        }
    }
}
```

### 4. Options Pattern for Configuration

```csharp
// 1. Define options class
public class MyOptions
{
    public const string SectionName = "My";
    
    public string Endpoint { get; set; } = "http://localhost";
    public int Timeout { get; set; } = 30;
}

// 2. Register in DI
services.Configure<MyOptions>(
    configuration.GetSection(MyOptions.SectionName));

// 3. Use in service
public class MyService
{
    private readonly MyOptions _options;

    public MyService(IOptions<MyOptions> options)
    {
        _options = options.Value;
    }
}

// 4. appsettings.json
{
    "My": {
        "Endpoint": "http://api.example.com",
        "Timeout": 60
    }
}
```

### 5. Interface Separation

```
Abstractions/           # Interfaces only, zero dependencies
├── IMyService.cs       # Interface definition
├── MyOptions.cs        # Configuration class
└── MyModels.cs         # Data models

Core/                   # Implementations
├── MyService.cs        # Service implementation
└── MyServiceExtensions.cs  # DI registration
```

## CSharpier Formatting

### Long Parameter Lists

```csharp
// ✅ Each parameter on its own line
public MyService(
    ILLMProvider llmProvider,
    IOptions<MyOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<MyService>? logger = null
)
{
    _llmProvider = llmProvider;
    _options = options.Value;
    _httpClient = httpClientFactory.CreateClient();
    _logger = logger ?? NullLogger<MyService>.Instance;
}
```

### Collection Initializers

```csharp
// ✅ Elements on separate lines with trailing comma
var messages = new List<ChatMessage>
{
    new("system", "You are a helpful assistant."),
    new("user", userInput),
};

var options = new LLMOptions
{
    Temperature = 0.7,
    MaxTokens = 1000,
    Model = "gpt-4",
};
```

### Method Chains

```csharp
// ✅ Each call on its own line
var result = items
    .Where(x => x.IsActive)
    .OrderBy(x => x.Name)
    .Select(x => new Result(x.Name, x.Value))
    .ToList();
```

### Always Use Braces

```csharp
// ✅ Always use braces
if (condition)
{
    DoSomething();
}

if (value == null)
{
    throw new ArgumentNullException(nameof(value));
}

// ❌ Never skip braces
if (condition)
    DoSomething(); // WRONG!
```

## XML Documentation

```csharp
/// <summary>
/// Processes the input and returns a result.
/// </summary>
/// <param name="input">The input to process.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The processing result.</returns>
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="input"/> is null.
/// </exception>
/// <exception cref="InvalidOperationException">
/// Thrown when processing fails.
/// </exception>
public async Task<Result> ProcessAsync(
    Input input,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(input);
    // ...
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
| DI | Microsoft.Extensions.DependencyInjection |
| Logging | Microsoft.Extensions.Logging |
| Config | Microsoft.Extensions.Options |
