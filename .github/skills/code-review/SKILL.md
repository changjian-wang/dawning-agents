---
description: |
  Use when: Reviewing code for architecture compliance, naming conventions, DI patterns, forbidden patterns, or best practices
  Don't use when: Writing new code or fixing bugs directly (use code-update); running tests (use run-tests); performing deep line-by-line audit (use deep-audit); checking for security vulnerabilities specifically (use security-audit)
  Inputs: Code files or PR diff to review
  Outputs: Structured review report with findings categorized by severity
  Success criteria: All findings documented with severity, location, and recommended fix
---

# Code Review Skill

## 交叉检查

审查时**必须**对照以下 skill 的关键规则：

- **`architecture`**：模块边界（Abstractions 不引用 Core）、命名空间规则
- **`code-update`**：禁止事项清单（8 条 ❌ 规则）
- **`csharpier`**：格式化规范

---

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

Order findings by severity: correctness > security > performance > maintainability.

