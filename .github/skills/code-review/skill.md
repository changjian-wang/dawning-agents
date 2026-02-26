---
description: "Review .NET code for Dawning.Agents project. Checks architecture compliance, naming, best practices. Trigger: 代码审查, review, 审查, code review, check code, 检查代码, PR review"
---

> **Skill 使用日志**：使用本 skill 后，在 `/memories/session/skill-log.md` 追加一行：`- {时间} code-review — {触发原因}`

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

### 6. 禁止事项检查（Forbidden Patterns）

- ❌ `new HttpClient()` — 必须 `IHttpClientFactory`
- ❌ `new XxxService()` — 必须 DI 注入
- ❌ 静态工厂创建服务
- ❌ `[Obsolete]` 标注
- ❌ `Abstractions/` 引用 `Core/`
- ❌ `IMapper` / AutoMapper
- ❌ 同步 I/O（`.Result`、`.Wait()`）
- ❌ 遗漏 `CancellationToken` 传递

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
