---
name: code-update
description: >
  Make code changes in Dawning.Agents following project patterns. Provides 
  templates for services, interfaces, DI extensions, and options classes.
  Use when asked to "modify code", "implement feature", "add method", "fix bug",
  "create class", or "update file".
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
- "Update the file to..."

## Before Making Changes

1. **Read context** - Understand related files first
2. **Check patterns** - Look at similar implementations
3. **Consider impact** - Check usages and dependencies

## Code Templates

### New Interface (Abstractions/)

```csharp
namespace Dawning.Agents.Abstractions;

/// <summary>
/// Brief description of the service.
/// </summary>
public interface IMyService
{
    /// <summary>
    /// Method description.
    /// </summary>
    /// <param name="input">Input description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Return description.</returns>
    Task<Result> DoSomethingAsync(
        Input input,
        CancellationToken cancellationToken = default);
}
```

### New Implementation (Core/)

```csharp
namespace Dawning.Agents.Core;

/// <summary>
/// Implementation of <see cref="IMyService"/>.
/// </summary>
public class MyService : IMyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService>? logger = null)
    {
        _logger = logger ?? NullLogger<MyService>.Instance;
    }

    /// <inheritdoc />
    public async Task<Result> DoSomethingAsync(
        Input input,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing {Input}...", input);
        
        // Implementation here
        
        return new Result();
    }
}
```

### New DI Extension

```csharp
namespace Dawning.Agents.Core;

public static class MyServiceExtensions
{
    /// <summary>
    /// Adds <see cref="IMyService"/> to the service collection.
    /// </summary>
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

/// <summary>
/// Configuration options for MyService.
/// </summary>
/// <remarks>
/// appsettings.json:
/// <code>
/// { "My": { "Option1": "value" } }
/// </code>
/// </remarks>
public class MyOptions
{
    public const string SectionName = "My";
    
    public string Option1 { get; set; } = "default";
}
```

## Guidelines

### Do ✅

- Use sufficient context in find-and-replace (3+ lines before/after)
- Follow CSharpier formatting
- Add XML documentation on public APIs
- Include `CancellationToken` in async methods
- Use `ILogger` with `NullLogger` fallback

### Don't ❌

- Create static factories or use `new` for services
- Hardcode configuration values
- Use `[Obsolete]` - delete old APIs directly
- Skip unit tests for new functionality

## After Changes

Run these commands to verify:

```powershell
dotnet build --nologo -v q    # Build
dotnet test --nologo          # Test
dotnet csharpier .            # Format
```
