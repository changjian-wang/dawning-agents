---
name: code-update
description: "Make code changes in Dawning.Agents with current patterns: DI, logging, cancellation, options validation, namespace rules, and templates."
---

# Code Update Skill

## What This Skill Does

Implements code changes using Dawning.Agents conventions so new code compiles, aligns with architecture, and passes review.

## When to Use

- "Modify this code"
- "Implement feature"
- "Add service/interface/options"
- "Fix bug"
- "Refactor module"

## Required Patterns

1. Pure DI via constructor injection
2. `ILogger<T>` with `NullLogger<T>.Instance` fallback when logger is optional
3. `CancellationToken cancellationToken = default` on async I/O methods
4. Public options implement `IValidatableOptions` with `Validate()`
5. Namespace follows subfolder path

## Namespace Convention

- `Dawning.Agents.Abstractions.{Area}`
- `Dawning.Agents.Core.{Area}`

## Templates

- `templates/interface-template.cs`
- `templates/service-template.cs`
- `templates/extensions-template.cs`
- `templates/options-template.cs`

## Post-change Checklist

- `dotnet build --nologo -v q`
- `dotnet test --nologo`
- `~/.dotnet/tools/csharpier format .`

## Common Pitfalls

- Wrong namespace root without area suffix
- Missing options validation
- Missing cancellation token
- Registering runtime services in `Abstractions`
