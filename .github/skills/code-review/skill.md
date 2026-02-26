---
name: code-review
description: "Review .NET code for Dawning.Agents project. Checks architecture compliance, CSharpier formatting, naming conventions, and best practices."
---

# Code Review Skill

## What This Skill Does

Reviews code changes against Dawning.Agents architecture, coding standards, and regression risk.

## Review Checklist

### 1. Architecture and DI

- Service contracts in `Abstractions`, implementations in `Core`/provider modules
- No static factories for runtime services
- Constructor injection used consistently
- Async methods include `CancellationToken`

### 2. Tool Interfaces (ISP)

- Read-only consumers depend on `IToolReader`
- Registration/setup code depends on `IToolRegistrar`
- `IToolRegistry` used only when both read/write are needed

### 3. Options and Validation

- Public options implement `IValidatableOptions`
- `Validate()` covers required fields and range checks
- Tests include valid/invalid cases for `Validate()`

### 4. Cost/Budget Safety

- Cost-sensitive flows use `ICostTracker` appropriately
- Budget overflow handled via `BudgetExceededException`
- Response/step cost fields remain consistent (`AgentStep.Cost`, `AgentResponse.TotalCost`)

### 5. Quality Gates

- Public APIs have XML docs
- Null handling uses guard clauses
- Logging is structured and meaningful
- Unit tests added for behavior changes
- CSharpier formatting passes

## Review Output Format

```markdown
## Findings
1. [Severity] file:line - issue

## Open Questions
1. ...

## Summary
- ...
```

Order findings by severity first: correctness > security > performance > maintainability.
