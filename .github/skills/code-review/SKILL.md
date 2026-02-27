---
description: "Review .NET code for Dawning.Agents project. Checks architecture compliance, naming, best practices. Trigger: 代码审查, review, 审查, code review, check code, 检查代码, PR review"
---

# Code Review Skill

## 目标

审查代码变更是否符合架构规范、编码标准和质量门禁。

## 触发条件

- **关键词**：代码审查, review, 审查, code review, check code, 检查代码, PR review
- **文件模式**：`*.cs`, `*.csproj`
- **用户意图**：审查代码质量、检查架构合规性、PR 审查

## 编排

- **前置**：`code-update`（代码变更完成后）
- **后续**：`build-project`（审查通过后构建验证）

## 交叉检查

审查时**必须**对照以下 skill 的关键规则：

- **`architecture`**：模块边界（Abstractions 不引用 Core）、命名空间规则
- **`code-update`**：禁止事项清单（8 条 ❌ 规则）
- **`csharpier`**：格式化规范

## Skill 使用日志

使用本 skill 后，在 `/memories/repo/skill-usage.md` 追加一行：`- {日期} code-review — {触发原因}`

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

## 验收场景

- **输入**："审查这次 PR 的代码变更"
- **预期**：agent 读取变更文件，按 6 项清单逐项检查，输出结构化报告
- **上次验证**：2026-02-27
