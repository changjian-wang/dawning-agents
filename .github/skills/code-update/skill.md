````skill
---
name: code-update
description: >
  Make code changes in Dawning.Agents following project patterns. Provides 
  templates for services, interfaces, DI extensions, and options classes.
  Includes .NET best practices: DI, logging, async, options pattern.
  Use when asked to "modify code", "implement feature", "add method", "fix bug",
  "create class", "update file", or "what's the best way to...".
---

# Code Update Skill

## What This Skill Does

Makes code changes in the Dawning.Agents project following established patterns and conventions.

## When to Use

- "Modify this code"
- "Implement this feature"
- "Add a new method/class"
- "Fix this bug"
- "Create a service for..."
- "What's the best way to..."
- "How should I implement..."

## Core Patterns

### 1. Pure Dependency Injection

```csharp
// ✅ Always use DI
public class MyService
{
    private readonly ILLMProvider _provider;
    
    public MyService(ILLMProvider provider) => _provider = provider;
}

// Usage
var service = serviceProvider.GetRequiredService<IMyService>();

// ❌ Never instantiate services directly
var provider = new OllamaProvider("model"); // WRONG!
```

### 2. Logger with NullLogger Fallback

```csharp
public MyService(ILogger<MyService>? logger = null)
{
    _logger = logger ?? NullLogger<MyService>.Instance;
}
```

### 3. CancellationToken on All Async Methods

```csharp
Task<Result> ProcessAsync(Input input, CancellationToken cancellationToken = default);
```

### 4. Options Pattern

```csharp
public class MyOptions { public const string SectionName = "My"; }
services.Configure<MyOptions>(configuration.GetSection(MyOptions.SectionName));
public MyService(IOptions<MyOptions> options) => _options = options.Value;
```

### 5. Interface Separation

```
Abstractions/  → Interfaces, Options, Models (zero deps)
Core/          → Implementations, DI Extensions
```

## Code Templates

### New Interface (Abstractions/)

```csharp
namespace Dawning.Agents.Abstractions;

/// <summary>
/// Brief description of the service.
/// </summary>
public interface IMyService
{
    Task<Result> DoSomethingAsync(
        Input input,
        CancellationToken cancellationToken = default);
}
```

### New Implementation (Core/)

```csharp
namespace Dawning.Agents.Core;

public class MyService : IMyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService>? logger = null)
    {
        _logger = logger ?? NullLogger<MyService>.Instance;
    }

    public async Task<Result> DoSomethingAsync(
        Input input,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing {Input}...", input);
        return new Result();
    }
}
```

### New DI Extension

```csharp
namespace Dawning.Agents.Core;

public static class MyServiceExtensions
{
    public static IServiceCollection AddMyService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MyOptions>(
            configuration.GetSection(MyOptions.SectionName));
        services.TryAddSingleton<IMyService, MyService>();
        return services;
    }
}
```

### New Options Class

```csharp
namespace Dawning.Agents.Abstractions;

public class MyOptions
{
    public const string SectionName = "My";
    public string Option1 { get; set; } = "default";
}
```

## Guidelines

### Do ✅

- Use sufficient context in find-and-replace (3+ lines before/after)
- Follow CSharpier formatting (see [csharpier skill](../csharpier/SKILL.md))
- Add XML documentation on public APIs
- Include `CancellationToken` in async methods
- Use `ILogger` with `NullLogger` fallback

### Don't ❌

- Create static factories or use `new` for services
- Hardcode configuration values
- Use `[Obsolete]` - delete old APIs directly
- Skip unit tests for new functionality

## After Changes

```bash
dotnet build --nologo -v q    # Build
dotnet test --nologo          # Test
dotnet csharpier .            # Format
```

## Tech Stack

| Category | Technology |
|----------|------------|
| Framework | .NET 10.0 |
| Testing | xUnit, FluentAssertions, Moq |
| Formatting | CSharpier |

````
