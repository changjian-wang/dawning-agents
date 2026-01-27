---
name: code-review
description: >
  Review .NET code for Dawning.Agents project. Checks architecture compliance 
  (DI, interface separation), CSharpier formatting, naming conventions, and 
  best practices. Use when asked to "review code", "check this code", "is this 
  code good", "code quality", or "PR review".
---

# Code Review Skill

## What This Skill Does

Reviews code changes in the Dawning.Agents project to ensure they follow established patterns, conventions, and best practices.

## When to Use

- "Review this code"
- "Check code quality"
- "Is this implementation correct?"
- "Review my PR/changes"
- "What's wrong with this code?"

## Review Checklist

### 1. Architecture Compliance

| Check | Requirement |
|-------|-------------|
| DI | All services via dependency injection, no `new` for services |
| Separation | Interfaces in `Abstractions/`, implementations in `Core/` |
| Logger | `ILogger<T>` with `NullLogger` fallback |
| Async | `CancellationToken` on all async methods |
| Config | `IOptions<T>` for configuration |

### 2. Code Style (CSharpier)

```csharp
// ✅ Long parameter lists: one per line
public MyService(
    ILLMProvider llmProvider,
    IOptions<MyOptions> options,
    ILogger<MyService>? logger = null
)
{
}

// ✅ Collections: trailing comma
var list = new List<string>
{
    "item1",
    "item2",
};

// ✅ Method chains: one call per line
var result = items
    .Where(x => x.IsActive)
    .Select(x => x.Name)
    .ToList();

// ✅ Always use braces
if (condition)
{
    DoSomething();
}
```

### 3. Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Interface | `I` prefix | `ILLMProvider` |
| Config | `Options` suffix | `LLMOptions` |
| DI extension | `Add` prefix | `AddLLMProvider` |
| Async method | `Async` suffix | `ChatAsync` |

### 4. Required Elements

- [ ] XML documentation on public APIs
- [ ] Unit tests for new functionality
- [ ] No hardcoded configuration values

## Output Format

Use this template for review results:

```markdown
## Code Review Summary

### ✅ Good
- [list positive aspects]

### ⚠️ Suggestions
- [suggestions with code examples]

### ❌ Issues (must fix)
- [critical issues]

### Overall: [Pass / Pass with suggestions / Needs changes]
```

## Example Review

**Input:** A new service class

**Output:**
```
## Code Review Summary

### ✅ Good
- Correct interface separation
- Proper DI pattern used

### ⚠️ Suggestions
- Add XML documentation to public methods
- Consider adding unit tests

### ❌ Issues
- Missing CancellationToken on async method

### Overall: Needs changes
```
