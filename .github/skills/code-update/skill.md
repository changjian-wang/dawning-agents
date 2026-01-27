---
name: code-update
description: Update code in Dawning.Agents project following established patterns
---

# Code Update Skill

Make code changes in the Dawning.Agents project following established patterns and conventions.

## When to Use

- When asked to modify, update, or change code
- When asked to implement a new feature
- When asked to fix a bug

## Before Making Changes

1. **Understand the context** - Read related files first
2. **Check existing patterns** - Look at similar implementations
3. **Consider impact** - Check for usages and dependencies

## Code Templates

### New Service Interface (in Abstractions/)

```csharp
namespace Dawning.Agents.Abstractions;

/// <summary>
/// Service description
/// </summary>
public interface IMyService
{
    Task<Result> DoSomethingAsync(
        Input input,
        CancellationToken cancellationToken = default);
}
```

### New Service Implementation (in Core/)

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
        _logger.LogDebug("Processing...");
        // Implementation
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

/// <summary>
/// Service configuration options
/// </summary>
public class MyOptions
{
    public const string SectionName = "My";
    
    public string Option1 { get; set; } = "default";
}
```

## Change Guidelines

### Do ✅

- Use find-and-replace with sufficient context (3+ lines before/after)
- Follow CSharpier formatting rules
- Add XML documentation on public APIs
- Include `CancellationToken` in async methods
- Use `ILogger` with `NullLogger` fallback

### Don't ❌

- Create static factories or allow direct `new` for services
- Hardcode configuration values
- Use `[Obsolete]` - delete old APIs directly
- Skip unit tests for new functionality

## After Making Changes

1. Verify build: `dotnet build`
2. Run tests: `dotnet test`
3. Format code: `dotnet csharpier .`
